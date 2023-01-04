Imports System.IO
Imports System.IO.Compression
Imports System.Threading
Imports AssetsTools.NET
Imports AssetsTools.NET.Extra
Imports Microsoft.Win32

Class MainWindow
    Public Property TextBoxWriter As ConsoleTextBoxWriter
    Public Property RunningThread As Thread

    Private Sub DropPanel_Drop(sender As Object, e As DragEventArgs)
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then
            Dim files As String() = e.Data.GetData(DataFormats.FileDrop)
            RunningThread = New Thread(Sub() HandleFileOpen(files(0)))
            RunningThread.Start()
        End If
    End Sub

    Private Sub HandleFileOpen(v As String)
        Me.Dispatcher.Invoke(Sub()
                                 DropPanel.AllowDrop = False
                                 DropPanel.IsEnabled = False
                             End Sub)

        Console.WriteLine("Attempting to open file as APK.")

        Dim extension = Path.GetExtension(v)
        Dim filename_noext = Path.GetFileNameWithoutExtension(v)

        If Not extension = ".apk" Then
            MsgBox("File must be an .apk file.", vbCritical, "Invalid file type.")
            Cleanup()
            Exit Sub
        End If

        Console.WriteLine("Extracting " & filename_noext & ".apk to ./" & filename_noext)

        Dim tempDir = Directory.CreateDirectory("./" & filename_noext)
        ZipFile.ExtractToDirectory(v, "./" & filename_noext)

        Console.WriteLine("Extracted " & filename_noext & ".apk to ./" & filename_noext)

        Dim bootConfigPath = Path.Combine("./" & filename_noext, "assets", "bin", "Data", "boot.config")

        Dim bootConfigContents = File.ReadAllText(bootConfigPath)

        bootConfigContents = bootConfigContents.Replace("vr-device-list=Oculus", "vr-device-list=")
        bootConfigContents = bootConfigContents.Replace("vr-enabled=1", "vr-enabled=0")

        File.WriteAllText(bootConfigPath, bootConfigContents)

        Console.WriteLine("Patched boot.config")

        Dim am = New AssetsManager()
        am.LoadClassPackage("classdata.tpk")

        Dim globalGameManagersPath = Path.Combine("./" & filename_noext, "assets", "bin", "Data", "globalgamemanagers")


        Console.WriteLine("Loading globalgamemanagers@BuildSettings")

        Dim globalGameManagers = am.LoadAssetsFile(globalGameManagersPath, False)
        am.LoadClassDatabaseFromPackage(globalGameManagers.file.typeTree.unityVersion)
        Dim buildSettingsInst = globalGameManagers.table.GetAssetsOfType(AssetClassID.BuildSettings).First()

        Console.WriteLine("Replacing globalgamemanagers@BuildSettings@enabledVrDevices with empty vector")
        Dim buildSettings = globalGameManagers.table.GetAssetsOfType(AssetClassID.BuildSettings).First
        Dim buildSettingsBase = am.GetTypeInstance(globalGameManagers.file, buildSettings).GetBaseField()
        Dim enabledVRDevices = buildSettingsBase.Get("enabledVRDevices").Get("Array")
        Dim vrDevicesList() As AssetTypeValueField = {}
        enabledVRDevices.SetChildrenList(vrDevicesList)

        Dim repl = New AssetsReplacerFromMemory(0, buildSettingsInst.index, buildSettingsInst.curFileType, &HFFFF, buildSettingsBase.WriteToByteArray())

        Dim stream = File.OpenWrite("globalgamemanagers-modified")
        Dim writer = New AssetsFileWriter(stream)
        Dim lAR = New List(Of AssetsReplacer)()
        lAR.Add(repl)
        globalGameManagers.file.Write(writer, 0, lAR, 0)
        writer.Close()
        stream.Close()

        am.UnloadAll()

        File.Copy("globalgamemanagers-modified", globalGameManagersPath, True)

        Console.WriteLine("Patched globalgamemanagers@BuildSettings@enabledVrDevices")

        Console.WriteLine("Replacing libovrplatformloader.so")

        Dim libOvrPath = Path.Combine("./" & filename_noext, "lib", "arm64-v8a", "libovrplatformloader.so")

        File.Copy("libovrplatformloader.so", libOvrPath, True)

        Console.WriteLine("Replaced libovrplatformloader.so")

        Console.WriteLine("Rebuilding " & filename_noext & ".apk (this may take a while)")

        File.Delete(filename_noext & ".apk")
        ZipFile.CreateFromDirectory("./" & filename_noext, filename_noext & ".apk")

        Console.WriteLine("Signing " & filename_noext & ".apk (this may take a while)")

        Dim apkSigner As Process = New Process()
        apkSigner.StartInfo.UseShellExecute = False
        apkSigner.StartInfo.CreateNoWindow = True
        apkSigner.StartInfo.RedirectStandardError = True
        apkSigner.StartInfo.RedirectStandardOutput = True
        apkSigner.StartInfo.FileName = "java"
        apkSigner.StartInfo.Arguments = "-jar ./uber-apk-signer.jar -a " & filename_noext & ".apk"

        AddHandler apkSigner.OutputDataReceived, New DataReceivedEventHandler(Sub(s, e) Console.WriteLine(e.Data))
        AddHandler apkSigner.ErrorDataReceived, New DataReceivedEventHandler(Sub(s, e) Console.WriteLine(e.Data))

        apkSigner.Start()
        apkSigner.BeginErrorReadLine()
        apkSigner.BeginOutputReadLine()
        apkSigner.WaitForExit()

        Console.WriteLine("Completed!")

        Cleanup()
    End Sub

    Private Sub Cleanup()
        Me.Dispatcher.Invoke(Sub()
                                 DropPanel.AllowDrop = True
                                 DropPanel.IsEnabled = True
                             End Sub)
        Try
            Directory.Delete("./temp-apk-unzipped")
        Catch ex As Exception
            ' nothin
        End Try
    End Sub

    Private Sub OnWindowLoaded(sender As Object, e As RoutedEventArgs)
        TextBoxWriter = New ConsoleTextBoxWriter(LogBox, Me.Dispatcher)
    End Sub

    Private Sub DropPanel_Click(sender As Object, e As RoutedEventArgs) Handles DropPanel.Click
        Dim dlg As OpenFileDialog = New OpenFileDialog()
        dlg.Filter = "APK Files (*.apk)|*.apk|All files (*.*)|*.*"

        If dlg.ShowDialog() Then
            RunningThread = New Thread(Sub() HandleFileOpen(dlg.FileName))
            RunningThread.Start()
        End If
    End Sub
End Class
