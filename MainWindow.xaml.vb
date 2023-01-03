Imports System.IO
Imports System.IO.Compression
Imports System.Text
Imports AssetsTools.NET
Imports AssetsTools.NET.Extra

Class MainWindow
    Private Sub ImagePanel_Drop(sender As Object, e As DragEventArgs)
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then
            Dim files As String() = e.Data.GetData(DataFormats.FileDrop)
            HandleFileOpen(files(0))
        End If
    End Sub

    Private Sub HandleFileOpen(v As String)
        ImagePanel.AllowDrop = False
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

        Dim bootConfigPath = Path.Combine("./" & filename_noext, "assets", "bin", "Data", "boot.config")

        Dim bootConfigContents = File.ReadAllText(bootConfigPath)

        bootConfigContents = bootConfigContents.Replace("vr-device-list=Oculus", "vr-device-list=")
        bootConfigContents = bootConfigContents.Replace("vr-enabled=1", "vr-enabled=0")

        File.WriteAllText(bootConfigPath, bootConfigContents)

        Log("Patched boot.config")

        Dim am = New AssetsManager()
        am.LoadClassPackage("classdata.tpk")

        Dim globalGameManagersPath = Path.Combine("./" & filename_noext, "assets", "bin", "Data", "globalgamemanagers")


        Log("Loading globalgamemanagers")

        Dim globalGameManagers = am.LoadAssetsFile(globalGameManagersPath, False)
        am.LoadClassDatabaseFromPackage(globalGameManagers.file.typeTree.unityVersion)
        Dim buildSettingsInst = globalGameManagers.table.GetAssetsOfType(AssetClassID.BuildSettings).First()

        Log("Replacing enabledVrDevices with empty vector")
        Dim buildSettings = globalGameManagers.table.GetAssetsOfType(AssetClassID.BuildSettings).First
        Dim buildSettingsBase = am.GetTypeInstance(globalGameManagers.file, buildSettings).GetBaseField()
        Dim enabledVRDevices = buildSettingsBase.Get("enabledVRDevices").Get("Array")
        Dim vrDevicesList() As AssetTypeValueField = {}
        enabledVRDevices.SetChildrenList(vrDevicesList)

        Dim repl = New AssetsReplacerFromMemory(0, buildSettingsInst.index, buildSettingsInst.curFileType, &HFFFF, buildSettingsBase.WriteToByteArray())

        Dim stream = File.OpenWrite("globalgamemanagers.assets")
        Dim writer = New AssetsFileWriter(stream)
        Dim lAR = New List(Of AssetsReplacer)()
        lAR.Add(repl)
        globalGameManagers.file.Write(writer, 0, lAR, 0)
        writer.Close()
        stream.Close()

        Log("Completed")

        Log("Extracted .apk to ./temp-apk-unzipped")

        Cleanup()
    End Sub

    Private Sub Cleanup()
        ImagePanel.AllowDrop = True
        Log("Drop .apk file here.")
        Try
            Directory.Delete("./temp-apk-unzipped")
        Catch ex As Exception
            ' nothin
        End Try
    End Sub

    Private Sub Log(v As String)
        LogInfo.Content = v
    End Sub
End Class
