Option Strict On
Imports System.IO
Imports System.Runtime.InteropServices
Structure AssetBundleFile
    Shared ReadOnly Property RawFile As String = "UnityRaw"
    Shared ReadOnly Property LZMACompressedFile As String = "UnityWeb"
    <FieldDisplayName("资源组文件头")>
    Dim BundleHeader As AssetBundleHeader
    <FieldDisplayName("资源数量")>
    Dim AssetsCount As Integer
    <FieldDisplayName("资源入口记录")>
    Dim Entries As List(Of AssetBundleEntry)
    <FieldDisplayName("序列化的资源")>
    Dim Assets As List(Of Asset)
    Sub New(FileName As String)
        Dim Strm As New FileStream(FileName, FileMode.Open)
        BundleHeader = New AssetBundleHeader(Strm)
        With New StreamHelper.BigendianBinaryReader(Strm)
            AssetsCount = .ReadInt32
            Entries = New List(Of AssetBundleEntry)(AssetsCount)
            For i As Integer = 1 To AssetsCount
                Entries.Add(New AssetBundleEntry(Strm))
            Next
            If AssetsCount > 0 Then
                Assets = New List(Of Asset)(AssetsCount)
                Dim i As Integer = 0
                For Each ent In Entries
                    Strm.Position = ent.Offset + BundleHeader.HeaderSize
                    Assets.Add(New Asset(Strm, ent.Offset + BundleHeader.HeaderSize))
                Next
            End If
            .Close()
        End With
    End Sub
    Public Structure AssetBundleEntry
        <FieldDisplayName("名称")>
        Dim Name As String
        ''' <summary>
        ''' 注意！！！需要加上Header.HeaderSize
        ''' </summary>
        <FieldDisplayName("相对于文件头的偏移")>
        Dim Offset As UInteger
        <FieldDisplayName("大小")>
        Dim Size As UInteger
        Sub New(Strm As Stream)
            With New StreamHelper.BigendianBinaryReader(Strm)
                Name = .ReadSinglebyteString(1).ToString
                Offset = .ReadUInt32
                Size = .ReadUInt32
            End With
        End Sub
    End Structure
    '16字节对齐
    ''' <summary>
    ''' AssetBundleHeader的字节顺序是Bigendian
    ''' </summary>
    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Ansi)>
    Structure AssetBundleHeader
        <FieldDisplayName("文件类型")>
        Dim Signature As String
        <FieldDisplayName("文件版本")>
        Dim StreamVersion As Integer
        <FieldDisplayName("引擎版本")>
        Dim UnityVersion As String
        <FieldDisplayName("引擎修订版本")>
        Dim UnityReversion As String
        <FieldDisplayName("最低获得的字节数")>
        Dim MinimumStreamedBytes As UInteger
        <FieldDisplayName("文件头大小")>
        Dim HeaderSize As UInteger
        <FieldDisplayName("下载层级数")>
        Dim NumberOfLevelsToDownload As Integer
        <FieldDisplayName("层级数")>
        Dim NumberOfLevels As Integer
        <FieldDisplayName("层级大小")>
        Dim LevelByteEnd As List(Of SizePair)
        <FieldDisplayName("完整文件大小")>
        Dim CompleteFileSize As UInteger?
        <FieldDisplayName("数据头大小")>
        Dim FileInfoHeaderSize As UInteger?
        <FieldDisplayName("是否压缩")>
        Dim Compressed As Byte
        Sub New(Strm As Stream)
            With New StreamHelper.BigendianBinaryReader(Strm)
                Signature = .ReadSinglebyteString(8).ToString
                If Not {RawFile, LZMACompressedFile}.Contains(Signature) Then
                    Throw New FileFormatException("不是能识别的AssetBundle文件")
                End If
                StreamVersion = .ReadInt32
                If StreamVersion < 1 OrElse StreamVersion > 3 Then
                    Throw New FileFormatException("AssetBundle文件版本不正确")
                End If
                UnityVersion = .ReadSinglebyteString(5).ToString
                UnityReversion = .ReadSinglebyteString(7).ToString
                MinimumStreamedBytes = .ReadUInt32
                HeaderSize = .ReadUInt32
                If HeaderSize > Strm.Length Then
                    Throw New FileFormatException("AssetBundle文件HeaderSize已损坏")
                End If
                NumberOfLevelsToDownload = .ReadInt32
                NumberOfLevels = .ReadInt32
                LevelByteEnd = New List(Of SizePair)(NumberOfLevels)
                For i As Integer = 0 To NumberOfLevels - 1
                    LevelByteEnd.Add(New SizePair(.ReadUInt32, .ReadUInt32))
                Next
                If StreamVersion >= 2 Then
                    CompleteFileSize = .ReadUInt32
                    If StreamVersion >= 3 Then
                        FileInfoHeaderSize = .ReadUInt32
                    End If
                End If
                Compressed = CByte(Strm.ReadByte)
            End With
        End Sub
        Public Function IsCompressed() As Boolean
            Return Signature <> RawFile
        End Function
        Public Structure SizePair
            <FieldDisplayName("在文件里的大小")>
            Dim Size As UInteger
            <FieldDisplayName("未压缩时的大小")>
            Dim DecompressedSize As UInteger
            Sub New(A As UInteger, B As UInteger)
                Size = A
                DecompressedSize = B
            End Sub
        End Structure
    End Structure
    Structure Asset
        <FieldDisplayName("资源头")>
        Dim Header As AssetHeader
        <FieldDisplayName("资源元数据")>
        Dim Meta As AssetMeta
        <FieldDisplayName("对象数据")>
        Dim Body As List(Of ObjectData)
        Sub New(Strm As Stream, HeaderAssetOfs As UInteger)
            With New StreamHelper.BigendianBinaryReader(Strm)
                Header = New AssetHeader(Strm)
                Meta = New AssetMeta(Strm, Header, HeaderAssetOfs + Header.DataOffset)
                Body = New List(Of ObjectData)(Meta.ObjectInfoCount)
                For Each ohd In Meta.ObjectHeaders
                    Body.Add(New ObjectData(Strm, ohd, HeaderAssetOfs + Header.DataOffset))
                Next
            End With
        End Sub
        Structure AssetHeader
            <FieldDisplayName("序列化的类型总大小")>
            Dim MetaTypesSize As Integer
            <FieldDisplayName("资源文件大小")>
            Dim AssetFileSize As Integer
            <FieldDisplayName("资源格式版本")>
            Dim FormatVersion As UInteger
            ''' <summary>
            ''' 注意！！！相对于全文件要先+HeaderSize+Entry.Offset
            ''' </summary>
            <FieldDisplayName("数据相对于资源起始的偏移量")>
            Dim DataOffset As UInteger
            <FieldDisplayName("字节顺序")>
            Dim Endianness As Integer
            Sub New(Strm As Stream)
                With New StreamHelper.BigendianBinaryReader(Strm)
                    MetaTypesSize = .ReadInt32
                    AssetFileSize = .ReadInt32
                    FormatVersion = .ReadUInt32
                    DataOffset = .ReadUInt32
                    Endianness = .ReadInt32
                End With
            End Sub
        End Structure
        Structure AssetMeta
            <FieldDisplayName("修正版本")>
            Dim Reversion As String
            <FieldDisplayName("类型树属性")>
            Dim TypetreeAttribute As Integer?
            <FieldDisplayName("类型树数量")>
            Dim TypetreeCount As Integer
            <FieldDisplayName("类型树")>
            Dim TypeTrees As TypeTreeGroup()
            <FieldDisplayName("几乎总是0")>
            Dim Blank As Integer
            <FieldDisplayName("对象数")>
            Dim ObjectInfoCount As Integer
            <FieldDisplayName("序列化的对象头")>
            Dim ObjectHeaders As ObjectHeader() '整体对齐到4096字节
            <FieldDisplayName("外部文件数量")>
            Dim ExternalCount As Integer
            <FieldDisplayName("外部文件信息")>
            Dim ExternalFiles As ExternalFile()
            <FieldDisplayName("对齐长度")>
            Dim AlignDataLength As Integer
            <DoNotDisplayMember, FieldDisplayName("16字节对齐")>
            Dim DataAlignment As Byte() '要做到16字节对齐。
            Sub New(Strm As Stream, RelevantHeader As AssetHeader, HeaderAssetAssetDataOffset As UInteger)
                Reversion = New StreamHelper.BigendianBinaryReader(Strm).ReadSinglebyteString(7).ToString 'ver已经>=9
                With New BinaryReader(Strm)
                    TypetreeAttribute = .ReadInt32 'ver已经>=9
                    TypetreeCount = .ReadInt32
                    ReDim TypeTrees(TypetreeCount - 1)
                    For i As Integer = 0 To TypeTrees.Length - 1
                        TypeTrees(i) = New TypeTreeGroup(Strm)
                    Next
                    Blank = .ReadInt32
                    ObjectInfoCount = .ReadInt32
                    If ObjectInfoCount > 0 Then
                        ReDim ObjectHeaders(ObjectInfoCount - 1)
                        For i As Integer = 0 To ObjectHeaders.Length - 1
                            ObjectHeaders(i) = New ObjectHeader(Strm)
                        Next
                    End If
                    ExternalCount = .ReadInt32
                    If ExternalCount > 0 Then
                        ReDim ExternalFiles(ExternalCount - 1)
                        For i As Integer = 0 To ExternalFiles.Length - 1
                            ExternalFiles(i) = New ExternalFile(Strm)
                        Next
                    End If
                    AlignDataLength = .ReadInt32
                    If AlignDataLength > 0 Then
                        ReDim DataAlignment(AlignDataLength - 1)
                        Strm.Read(DataAlignment, 0, AlignDataLength)
                    End If
                End With
            End Sub
            Structure TypeTreeGroup
                <FieldDisplayName("类型标识符")>
                Dim CLSID As Integer
                <FieldDisplayName("类型树的根元素")>
                Dim TreeRoot As TypeTree
                Sub New(strm As Stream)
                    With New BinaryReader(strm)
                        CLSID = .ReadInt32
                        TreeRoot = New TypeTree(strm)
                    End With
                End Sub
                Structure TypeTree
                    <FieldDisplayName("类型名称")>
                    Dim TypeName As String
                    <FieldDisplayName("变量名称")>
                    Dim VarName As String
                    <FieldDisplayName("大小")>
                    Dim Size As Integer
                    <FieldDisplayName("索引")>
                    Dim Index As Integer
                    <FieldDisplayName("此类型是数组")>
                    Dim IsArray As Integer
                    <FieldDisplayName("版本")>
                    Dim Version As Integer
                    <FieldDisplayName("元数据标识")>
                    Dim MetaFlag As Integer
                    <FieldDisplayName("子节点数量")>
                    Dim ChildCount As Integer
                    <FieldDisplayName("子节点")>
                    Dim Child As List(Of TypeTree)
                    Sub New(Strm As Stream)
                        With New StreamHelper.BigendianBinaryReader(Strm)
                            TypeName = .ReadSinglebyteString(4).ToString
                            VarName = .ReadSinglebyteString(6).ToString
                        End With
                        With New BinaryReader(Strm)
                            Size = .ReadInt32
                            Index = .ReadInt32
                            IsArray = .ReadInt32
                            Version = .ReadInt32
                            MetaFlag = .ReadInt32
                            ChildCount = .ReadInt32
                            Child = New List(Of TypeTree)(ChildCount)
                            For i As Integer = 0 To ChildCount - 1
                                Child.Add(New TypeTree(Strm))
                            Next
                        End With
                    End Sub
                End Structure
            End Structure

            Structure ObjectHeader
                <FieldDisplayName("路径ID")>
                Dim PathID As Integer
                ''' <summary>
                ''' 注意！！！全文件偏移要+HeaderSize+AssetOffset+AssetDataOffset
                ''' </summary>
                <FieldDisplayName("相对于资源数据偏移的偏移")>
                Dim Offset As UInteger
                <FieldDisplayName("长度")>
                Dim Length As Integer
                <FieldDisplayName("路径ID")>
                Dim TypeID As Integer
                <FieldDisplayName("类ID")>
                Dim ClassID As Short
                <FieldDisplayName("已释放")>
                Dim Disposed As Short
                Sub New(Strm As Stream)
                    With New BinaryReader(Strm)
                        PathID = .ReadInt32
                        Offset = .ReadUInt32
                        Length = .ReadInt32
                        TypeID = .ReadInt32
                        ClassID = .ReadInt16
                        Disposed = .ReadInt16
                    End With
                End Sub
            End Structure
            Structure ExternalFile
                <FieldDisplayName("Asset路径")>
                Dim AssetPath As String
                <FieldDisplayName("标识符")>
                Dim GUID As Guid
                <FieldDisplayName("类型ID")>
                Dim TypeID As Integer
                <FieldDisplayName("文件路径")>
                Dim FilePath As String
                Sub New(Strm As Stream)
                    With New StreamHelper.BigendianBinaryReader(Strm)
                        AssetPath = .ReadSinglebyteString(0).ToString
                        Dim Buf(15) As Byte
                        Strm.Read(Buf, 0, 16)
                        GUID = New Guid(Buf)
                        TypeID = .ReadInt32
                        FilePath = .ReadSinglebyteString(0).ToString
                    End With
                End Sub
            End Structure
        End Structure

        Structure ObjectData
            <DoNotDisplayMember> <FieldDisplayName("对象数据")>
            Dim DataOfObject As Byte()
            <FieldDisplayName("对齐长度")>
            Dim AlignDataLength As Integer?
            <DoNotDisplayMember> <FieldDisplayName("用于对齐到8字节")>
            Dim DataAlignment As Byte()
            Sub New(Strm As Stream, RelevantHeader As AssetMeta.ObjectHeader, HeaderAssetAssetDataOffset As UInteger)
                With New BinaryReader(Strm)
                    If RelevantHeader.Length > 0 Then
                        Strm.Position = RelevantHeader.Offset + HeaderAssetAssetDataOffset
                        ReDim DataOfObject(RelevantHeader.Length - 1)
                        Strm.Read(DataOfObject, 0, RelevantHeader.Length)
                    End If
                    If Not Strm.Position >= Strm.Length - 3 Then
                        AlignDataLength = .ReadInt32
                        If AlignDataLength > 0 Then
                            ReDim DataAlignment(AlignDataLength.Value - 1)
                            Strm.Read(DataAlignment, 0, AlignDataLength.Value)
                        End If
                    End If
                End With
            End Sub
        End Structure
    End Structure
End Structure