Option Strict On
Imports System.Reflection
Imports System.Text
Public Class TypeTreeParser
    Private Shared TypeToVB As New Dictionary(Of String, String) From {{"Int", "Integer"},
            {"UInt", "UInteger"}, {"UInt8", "Byte"}, {"Bool", "Boolean"}, {"Float", "Single"},
            {"SInt8", "SByte"}, {"SInt16", "Short"}, {"UInt16", "UShort"}, {"SInt32", "Integer"},
            {"UInt64", "ULong"}, {"SInt64", "Long"}}
    Private Shared ConflictTypes As New Dictionary(Of String, String) From {{"Object", "[Object]"},
        {"Date", "[Date]"}}
    Private Shared VBPreservedNames As IEnumerable(Of String) = {"Namespace",
        "End", "Class", "Module", "Sub", "Function", "Partial", "New", "Integer", "UInteger", "Byte",
        "Boolean", "Single", "SByte", "Short", "Long", "Object", "String", "Double", "Overrides", "Overloads",
        "AddressOf", "NameOf", "Shadows", "Private", "Public", "Friend", "Shared", "Imports", "As", "Of",
        "Static", "AddHandler", "RemoveHandler", "Date", "Operator", "CType", "DirectCast", "TryCast", "Do",
        "Narrowing", "If", "Widening", "ByRef", "ByVal", "In", "Out", "Get", "Set", "Property", "For", "Next",
        "TypeOf", "Is", "IsNot", "Let", "GetType", "Like", "Each", "ElseIf", "Else", "Select", "Case", "Exit",
        "Continue", "CInt", "CObj", "CStr", "CByte", "CSng", "CDbl", "CUInt", "CSByte", "CDate", "CBool", "CShort",
        "CLng", "CDec", "True", "False", "Nothing", "NotInheritable", "MustInherit", "Inherits", "Then", "Try",
        "Catch", "Finally", "Overridable"}
    Private Shared Function GetWriteCommand(VarName As String, TypeName As String, StrmName As String, IsArray As Boolean, Indent As Integer) As String
        If IsArray Then
            Return $"For i As Integer = 0 To {VarName}.Length - 1
{Space(Indent + InP)}{GetWriteCommand(VarName + "(i)", TypeName, StrmName, False, Indent)}
{Space(Indent)}Next"
        Else
            If TypeToVB.Values.Contains(TypeName) Then
                Return $".Write({VarName})"
            ElseIf TypeName = "String"
                Return $"WriteAnsiBStr({StrmName}, {VarName})"
            Else
                Return $"{VarName}.Serialize({StrmName})"
            End If
        End If
    End Function
    Private Shared Function NameReplace(Name As String) As String
        Return Name.Replace(" ", "_").Replace("$", "_")
    End Function
    Private Shared Function NormalizeFieldName(FieldName As String) As String
        Dim IsArray = FieldName.Contains("()")
        If IsArray Then
            FieldName = FieldName.Substring(0, FieldName.Length - 2)
        End If
        For Each name In VBPreservedNames
            If StrComp(FieldName, name, CompareMethod.Text) = 0 Then
                Return "[" + NameReplace(FieldName) + "]" + If(IsArray, "()", String.Empty)
            End If
        Next
        Return NameReplace(FieldName) + If(IsArray, "()", String.Empty)
    End Function
    Private Shared Function IsWeakEqualTree(Tree1 As AssetBundleFile.Asset.TypeTreeGroup.TypeTree, Tree2 As AssetBundleFile.Asset.TypeTreeGroup.TypeTree) As Boolean
        If Tree1.ChildCount = Tree2.ChildCount AndAlso Tree1.IsArray = Tree2.IsArray AndAlso
           Tree1.Size = Tree2.Size AndAlso Tree1.TypeName = Tree2.TypeName Then
            For i As Integer = 0 To Tree1.ChildCount - 1
                If Not IsWeakEqualTree(Tree1.Child(i), Tree2.Child(i)) Then
                    Return False
                End If
            Next
            Return True
        End If
        Return False
    End Function
    Private Shared Function TypeNameReplaceForCombinedTypeName(TypeName As String) As String
        If TypeName.Contains("(Of ") AndAlso Not TypeName.EndsWith(")") Then
            TypeName = TypeName.Replace("(Of ", "Of").Replace(")", "")
        End If
        Return TypeName.Replace("[", "").Replace("]", "").Replace("T As ", "")
    End Function
    Private Shared Function NormalizeTypeName(TypeName As String, IsStructureIdentifier As Boolean, TypeTree? As AssetBundleFile.Asset.TypeTreeGroup.TypeTree) As String
        If String.IsNullOrEmpty(TypeName) Then
            Return String.Empty
        End If
        If TypeName = "Array" Then
            If TypeTree.HasValue Then
                If TypeTree.Value.ChildCount >= 2 Then
                    Return TypeNameReplaceForCombinedTypeName(NormalizeTypeName(TypeTree.Value.Child(1).TypeName, IsStructureIdentifier, TypeTree.Value.Child(1)) + TypeName)
                End If
            Else
                Return "[Array]"
            End If
        ElseIf TypeName = "map"
            If TypeTree.HasValue Then
                If TypeTree.Value.ChildCount >= 1 Then
                    If TypeTree.Value.Child(0).ChildCount >= 2 Then
                        Return TypeNameReplaceForCombinedTypeName(NormalizeTypeName(TypeTree.Value.Child(0).Child(1).TypeName, IsStructureIdentifier, TypeTree.Value.Child(0).Child(1)) + "Map")
                    End If
                End If
            End If
        ElseIf TypeName = "pair"
            If TypeTree.HasValue Then
                If TypeTree.Value.ChildCount >= 2 Then
                    Return TypeNameReplaceForCombinedTypeName(NormalizeTypeName(TypeTree.Value.Child(0).TypeName, IsStructureIdentifier, TypeTree.Value.Child(0)) + NormalizeTypeName(TypeTree.Value.Child(1).TypeName, IsStructureIdentifier, TypeTree.Value.Child(1)) + "Pair")
                End If
            End If
        ElseIf TypeName = "vector"
            If TypeTree.HasValue Then
                If TypeTree.Value.ChildCount >= 1 Then
                    Return TypeNameReplaceForCombinedTypeName(NormalizeTypeName(TypeTree.Value.Child(0).TypeName, IsStructureIdentifier, TypeTree.Value.Child(0)) + "Vector")
                End If
            End If
        End If
        Dim IsUnsigned = TypeName.Contains("unsigned ")
        If IsUnsigned Then
            TypeName = TypeName.Replace("unsigned ", "u")
        End If
        If TypeName.Contains("<") AndAlso TypeName.Contains(">") Then
            Dim lt = TypeName.IndexOf("<")
            Dim tn = NormalizeTypeName(TypeName.Substring(0, lt), False, Nothing)
            Dim gn = NormalizeTypeName(TypeName.Substring(lt + 1, TypeName.IndexOf(">") - lt - 1), False, Nothing)
            Return tn + "(Of " + If(IsStructureIdentifier, "T As ", String.Empty) + gn + ")"
        End If
        Dim ChrData = TypeName.ToCharArray
        ChrData(0) = ChrData(0).ToString.ToUpper.First
        If IsUnsigned Then
            ChrData(1) = ChrData(1).ToString.ToUpper.First
        End If
        Dim Ori As String = New String(ChrData)
        If TypeToVB.ContainsKey(Ori) Then
            Return NameReplace(TypeToVB(Ori))
        ElseIf ConflictTypes.ContainsKey(Ori)
            Return NameReplace(ConflictTypes(Ori))
        Else
            Return NameReplace(Ori)
        End If
    End Function
    Friend Shared Function ToVBCodes(TypeTreeGroup As IEnumerable(Of AssetBundleFile.Asset.TypeTreeGroup)) As String()
        Dim Codes(TypeTreeGroup.Count - 1) As String
        For i As Integer = 0 To Codes.Length - 1
            Codes(i) = ToVBCode(TypeTreeGroup(i).TreeRoot)
        Next
        Return Codes
    End Function
    Private Shared Function GetReadCommand(VarName As String, VBTypeName As String, IsArray As Boolean,
            StreamName As String, Optional SizeName As String = Nothing, Optional Indent As Integer = 0) As String
        If IsArray Then
            If String.IsNullOrEmpty(SizeName) Then
                Throw New ArgumentNullException(NameOf(SizeName))
            End If
            Return $"Redim {VarName}({SizeName} - 1)
{Space(Indent)}If {SizeName} > 0 Then
{Space(Indent + InP)}For i As Integer = 0 To {SizeName} - 1
{Space(Indent + InP + InP)}{VarName}(i) = {GetReadCommand(VarName, VBTypeName, False, StreamName)}
{Space(Indent + InP)}Next
{Space(Indent)}End If"
        Else
            Select Case VBTypeName
                Case "UInteger", "UInt32"
                    Return ".ReadUInt32"
                Case "Integer"
                    Return ".ReadInt32"
                Case "UShort", "UInt16"
                    Return ".ReadUInt16"
                Case "Short"
                    Return ".ReadInt16"
                Case "SByte"
                    Return ".ReadSByte"
                Case "Single", "Double", "Decimal", "Boolean", "Char", "Byte"
                    Return ".Read" + VBTypeName
                Case "String"
                    Return $"ReadAnsiBStr({StreamName})"
            End Select
        End If
        Return $"New {VBTypeName}({StreamName})"
    End Function
    Friend Shared Function GetMarshalAs(VBTypeName As String, IsArray As Boolean, Optional SizeConst As Integer = 0) As String
        If IsArray Then
            Select Case VBTypeName
                Case "Char"
                    Return $"<MarshalAs(UnmanagedType.ByValTStr,SizeConst:={SizeConst})>"
                Case "Boolean"
                    Return $"<MarshalAs(UnmanagedType.ByValArray,ArraySubType:=UnmanagedType.U1,SizeConst:={SizeConst}))>"
                Case Else
                    Return $"<MarshalAs(UnmanagedType.ByValArray,SizeConst:={SizeConst}))>"
            End Select
        Else
            Select Case VBTypeName
                Case "Boolean"
                    Return "<MarshalAs(UnmanagedType.U1)>"
                Case Else
                    Return String.Empty
            End Select
        End If
    End Function
    Const InP = 4
    Friend Shared Function ToVBCode(TypeTreeRoot As AssetBundleFile.Asset.TypeTreeGroup.TypeTree) As String
        Dim indent As Integer = 4
        TypeTreeRoot.TypeName = NormalizeTypeName(TypeTreeRoot.TypeName, False, TypeTreeRoot)
        Dim tpinf As New StringBuilder($"'VB代码，最低版本要求是2010，使用默认的编译选项，.net framework版本最低3.0。
'类型树经过合并和重命名来达到较好的阅读效果
Imports System.IO
Imports System.Text
<CodeDom.Compiler.GeneratedCode(""{Assembly.GetExecutingAssembly.FullName}"", ""{DirectCast(Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly, GetType(AssemblyFileVersionAttribute)), AssemblyFileVersionAttribute).Version}"")>
Class TypeTree
{Space(indent)}Sub New(DataStrm As Stream)
{Space(indent + InP)}{NormalizeFieldName(TypeTreeRoot.VarName)}=New {TypeTreeRoot.TypeName}(DataStrm)
{Space(indent)}End Sub
")
        Dim ReadBStr As Boolean = False
        Dim PrintChild As Action(Of AssetBundleFile.Asset.TypeTreeGroup.TypeTree, Boolean)
        PrintChild = Sub(tree, SuppressStructureDeclare)
                         With tree
                             tpinf.Append(Space(indent))
                             Dim TpName = NormalizeTypeName(.TypeName, False, tree)
                             tpinf.AppendLine($"Dim { NormalizeFieldName(.VarName) } As { TpName }")
                             If TpName = "String" Then
                                 ReadBStr = True
                             ElseIf SuppressStructureDeclare
                             Else
                                 If .ChildCount > 0 Then
                                     tpinf.Append(Space(indent)).Append("Structure ")
                                     tpinf.AppendLine(NormalizeTypeName(.TypeName, True, tree))
                                     indent += InP
                                     Dim Counter As Integer = 0
                                     Dim LastSize As Integer = 0
                                     Dim Ctor As New List(Of String)
                                     Dim Serializer As New List(Of String)
                                     Dim ArrVarName As String = String.Empty
                                     Dim UsedUnknownTypeName As New List(Of String)(.ChildCount)
                                     Dim RenameTypeId As Integer = 0
                                     Dim DeclaredTrees As New List(Of AssetBundleFile.Asset.TypeTreeGroup.TypeTree)(.ChildCount)
                                     For Each ch In .Child
                                         Dim CurrentFieldName = NormalizeFieldName(ch.VarName)
                                         Dim CurrentTypeName = NormalizeTypeName(ch.TypeName, False, ch)
                                         Dim SuppressNextTreeStructDecl As Boolean = False
                                         For Each tr In DeclaredTrees
                                             If IsWeakEqualTree(tr, ch) Then
                                                 SuppressNextTreeStructDecl = True
                                                 Exit For
                                             End If
                                         Next
                                         DeclaredTrees.Add(ch)
                                         If Not SuppressNextTreeStructDecl AndAlso Not TypeToVB.Values.Contains(CurrentTypeName) Then
                                             If UsedUnknownTypeName.Contains(CurrentTypeName) Then
                                                 CurrentTypeName += "_" + RenameTypeId.ToString
                                                 ch.TypeName = CurrentTypeName
                                                 RenameTypeId += 1
                                             End If
                                             UsedUnknownTypeName.Add(CurrentTypeName)
                                         End If
                                         If Counter > 0 Then
                                             Ctor.Add(GetReadCommand(CurrentFieldName, CurrentTypeName, True, "Strm", ArrVarName, indent + InP * 2))
                                             Serializer.Add(GetWriteCommand(CurrentFieldName, CurrentTypeName, "Strm", True, indent + InP * 2))
                                             ch.VarName += "()"
                                         Else
                                             Serializer.Add(GetWriteCommand(CurrentFieldName, CurrentTypeName, "Strm", False, indent + InP * 2))
                                             Ctor.Add(CurrentFieldName + " = " + GetReadCommand(CurrentFieldName, CurrentTypeName, False, "Strm", Indent:=indent))
                                         End If
                                         PrintChild(ch, SuppressNextTreeStructDecl)
                                         If CBool(.IsArray) Then
                                             Counter += 1
                                             ArrVarName = CurrentFieldName
                                         End If
                                     Next
                                     tpinf.Append(Space(indent)).AppendLine("Sub New(Strm As Stream)")
                                     indent += InP
                                     Dim NeedWith As Boolean = False
                                     Dim MayUseCall As Boolean = Ctor.Count = 1
                                     Dim ExplicitEncoding As String = String.Empty
                                     For Each ct In Ctor
                                         If ct.Contains("Char") Then
                                             ExplicitEncoding = ", Encoding.ASCII"
                                             Exit For
                                         End If
                                     Next
                                     For Each ct In Ctor
                                         If ct.Contains(".Read") Then
                                             NeedWith = True
                                             Exit For
                                         End If
                                     Next
                                     If MayUseCall Then
                                         If NeedWith Then
                                             tpinf.Append(Space(indent)).AppendLine(Ctor.First.Replace(".Read", $"New BinaryReader(Strm{ExplicitEncoding}).Read"))
                                         Else
                                             tpinf.Append(Space(indent)).AppendLine(Ctor.First)
                                         End If
                                     Else
                                         If NeedWith Then
                                             tpinf.Append(Space(indent)).AppendLine($"With New BinaryReader(Strm{ExplicitEncoding})")
                                             indent += InP
                                         End If
                                         For Each s In Ctor
                                             tpinf.Append(Space(indent)).AppendLine(s)
                                         Next
                                         If NeedWith Then
                                             indent -= InP
                                             tpinf.Append(Space(indent)).AppendLine("End With")
                                         End If
                                     End If
                                     indent -= InP
                                     tpinf.Append(Space(indent)).AppendLine("End Sub")
                                     tpinf.Append(Space(indent)).AppendLine("Sub Serialize(Strm As Stream)")
                                     indent += InP
                                     If MayUseCall Then
                                         If NeedWith Then
                                             tpinf.Append(Space(indent)).AppendLine(Serializer.First.Replace(".Write", $"New BinaryWriter(Strm{ExplicitEncoding}).Write"))
                                         Else
                                             tpinf.Append(Space(indent)).AppendLine(Serializer.First)
                                         End If
                                     Else
                                         If NeedWith Then
                                             tpinf.Append(Space(indent)).AppendLine($"With New BinaryWriter(Strm{ExplicitEncoding})")
                                             indent += InP
                                         End If
                                         For Each se In Serializer
                                             tpinf.Append(Space(indent)).AppendLine(se)
                                         Next
                                         If NeedWith Then
                                             indent -= InP
                                             tpinf.Append(Space(indent)).AppendLine("End With")
                                         End If
                                     End If
                                     indent -= InP
                                     tpinf.Append(Space(indent)).AppendLine("End Sub")
                                     indent -= InP
                                     tpinf.Append(Space(indent)).AppendLine("End Structure")
                                 End If
                             End If
                         End With
                     End Sub
        PrintChild(TypeTreeRoot, False)
        If ReadBStr Then
            tpinf.Append(Space(indent)).AppendLine("Function ReadAnsiBStr(Strm As Stream) As String")
            indent += InP
            tpinf.Append(Space(indent)).AppendLine("With New BinaryReader(Strm, Encoding.ASCII)")
            indent += InP
            tpinf.Append(Space(indent)).AppendLine("Dim Chrs(.ReadInt32 - 1) As Char")
            tpinf.Append(Space(indent)).AppendLine("For i As Integer = 0 To Chrs.Length - 1")
            indent += InP
            tpinf.Append(Space(indent)).AppendLine("Chrs(i) = .ReadChar")
            indent -= InP
            tpinf.Append(Space(indent)).AppendLine("Next")
            tpinf.Append(Space(indent)).AppendLine("Return New String(Chrs)")
            indent -= InP
            tpinf.Append(Space(indent)).AppendLine("End With")
            indent -= InP
            tpinf.Append(Space(indent)).AppendLine("End Function")
            tpinf.Append(Space(indent)).AppendLine("Sub WriteAnsiBStr(Strm As Stream, Str As String)")
            indent += InP
            tpinf.Append(Space(indent)).AppendLine("With New BinaryWriter(Strm, Encoding.ASCII)")
            indent += InP
            tpinf.Append(Space(indent)).AppendLine(".Write(Str.Length)")
            tpinf.Append(Space(indent)).AppendLine("For i As Integer = 0 To Str.Length - 1")
            indent += InP
            tpinf.Append(Space(indent)).AppendLine(".Write(Str(i))")
            indent -= InP
            tpinf.Append(Space(indent)).AppendLine("Next")
            tpinf.Append(Space(indent)).AppendLine(".Write(CByte(0))")
            indent -= InP
            tpinf.Append(Space(indent)).AppendLine("End With")
            indent -= InP
            tpinf.Append(Space(indent)).AppendLine("End Function")
            indent -= InP
        End If
        tpinf.AppendLine("End Class")
        Return tpinf.ToString
    End Function
End Class