
Imports System.Text

Class MainWindow
    Dim AssetBundle As AssetBundleFile
    Private Sub Load(fn As String)
        AssetBundle = New AssetBundleFile(fn)
        LstInfo.ItemsSource = FieldDisplayNameAttribute.GetNameValuesFromType(AssetBundle)
    End Sub
    Dim fn As String
    Private Sub LstInfo_Drop(sender As Object, e As DragEventArgs) Handles LstInfo.Drop
        fn = DirectCast(e.Data.GetData(DataFormats.FileDrop), String()).First
        Load(fn)
    End Sub
    Dim Processing As Boolean = False
    Dim Lock As New Object
    Private Async Function ProcessFileAsync() As Task
        Await Task.Run(Sub()
                           SyncLock Lock
                               If Processing Then
                                   Return
                               End If
                               Processing = True
                           End SyncLock
                           If AssetBundle.BundleHeader.Signature IsNot Nothing Then
                               Dim BaseDir = fn.Substring(0, fn.LastIndexOf("."c))
                               If Not IO.Directory.Exists(BaseDir) Then
                                   IO.Directory.CreateDirectory(BaseDir)
                               End If
                               For j As Integer = 0 To AssetBundle.Assets.Count - 1
                                   Dim assets = AssetBundle.Assets(j)
                                   Dim Codes = TypeTreeParser.ToVBCodes(assets.Meta.TypeTrees)
                                   For i As Integer = 0 To Codes.Count - 1
                                       Dim fn = $"{BaseDir}\AssetID{j}CodeData{i}.bin"
                                       IO.File.WriteAllText(fn + ".vb", Codes(i))
                                   Next
                                   For i As Integer = 0 To assets.Body.Count - 1
                                       Dim fn = $"{BaseDir}\AssetID{j}Data{i}.bin"
                                       IO.File.WriteAllBytes(fn, assets.Body(i).DataOfObject)
                                   Next
                               Next
                           End If
                           SyncLock Lock
                               Processing = False
                           End SyncLock
                       End Sub)
    End Function
    Private Async Sub w8btn_PreviewMouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs)
        Dim btn = DirectCast(sender, FrameworkElement)
        btn.IsEnabled = False
        TxtStatus.Text = "处理中..."
        Await ProcessFileAsync()
        TxtStatus.Text = String.Empty
        btn.IsEnabled = True
    End Sub
End Class
