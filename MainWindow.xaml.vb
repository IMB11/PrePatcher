Imports System.IO
Imports System.IO.Compression
Imports System.Text
Imports System.Threading
Imports AssetsTools.NET
Imports AssetsTools.NET.Extra

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
        DropPanel.AllowDrop = False
        DropPanel.IsEnabled = False
        Log("Attempting to open apk.")

        Dim extension = Path.GetExtension(v)
        Dim filename_noext = Path.GetFileNameWithoutExtension(v)

        If Not extension = ".apk" Then
            MsgBox("File must be an .apk file.", vbCritical, "Invalid file type.")
            Cleanup()
            Exit Sub
        End If

        Dim tempDir = Directory.CreateDirectory("./" & filename_noext)
        ZipFile.ExtractToDirectory(v, "./" & filename_noext)

        Log("Extracted .apk to ./temp-apk-unzipped")

        Dim bootConfigPath = Path.Combine("./" & filename_noext, "assets", "bin", "Data", "boot.config")

        Dim bootConfigContents = File.ReadAllText(bootConfigPath)

        bootConfigContents = bootConfigContents.Replace("vr-device-list=Oculus", "vr-device-list=")
        bootConfigContents = bootConfigContents.Replace("vr-enabled=1", "vr-enabled=0")

        File.WriteAllText(bootConfigPath, bootConfigContents)

        Log("Patched boot.config")

        Dim am = New AssetsManager()
        am.LoadClassPackage("classdata.tpk")

        Dim globalGameManagersPath = Path.Combine("./" & filename_noext, "assets", "bin", "Data", "globalgamemanagers")


        Log("Loading globalgamemanagers@BuildSettings")

        Dim globalGameManagers = am.LoadAssetsFile(globalGameManagersPath, False)
        am.LoadClassDatabaseFromPackage(globalGameManagers.file.typeTree.unityVersion)
        Dim buildSettingsInst = globalGameManagers.table.GetAssetsOfType(AssetClassID.BuildSettings).First()

        Log("Replacing globalgamemanagers@BuildSettings@enabledVrDevices with empty vector")
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

        File.Copy("globalgamemanagers-modified", globalGameManagersPath, True)

        Log("Patched globalgamemanagers@BuildSettings@enabledVrDevices")

        Log("Replacing libovrplatformloader.so")

        Dim libOvrPath = Path.Combine("./" & filename_noext, "lib", "arm64-v8a", "libovrplatformloader.so")

        File.Copy("libovrplatformloader.so", libOvrPath, True)

        Log("Replaced libovrplatformloader.so")

        Log("Rebuilding .apk")

        ZipFile.CreateFromDirectory("./" & filename_noext, filename_noext & "_unsigned.apk")

        Log("Signing .apk")

        Dim apkSignerJar = New Process()
        apkSignerJar.StartInfo.UseShellExecute = False
        apkSignerJar.StartInfo.FileName = "java"
        apkSignerJar.StartInfo.Arguments = "-jar uber-apk-signer -a " & filename_noext & "_unsigned.apk" & " --out " & filename_noext & "_patched.apk"
        apkSignerJar.Start()

        Log("Saving .apk to " & filename_noext & "_patched.apk")

        Log("Completed!")

        Cleanup()
    End Sub

    Private Sub Cleanup()
        DropPanel.AllowDrop = True
        DropPanel.IsEnabled = True
        Try
            Directory.Delete("./temp-apk-unzipped")
        Catch ex As Exception
            ' nothin
        End Try
    End Sub

    Private Sub Log(v As String)
        Console.WriteLine("> " & v)
    End Sub

    Private Sub OnWindowLoaded(sender As Object, e As RoutedEventArgs)
        TextBoxWriter = New ConsoleTextBoxWriter(LogBox)
    End Sub
End Class
