Option Strict On
Imports System.Reflection

<AttributeUsage(AttributeTargets.Field)>
Public NotInheritable Class FieldDisplayNameAttribute
    Inherits Attribute
    Public ReadOnly Name As String
    ''' <summary>
    ''' 获取字段的成员，非泛型可枚举成员，泛型可枚举成员
    ''' </summary>
    ''' <param name="Instance"></param>
    ''' <returns></returns>
    Public Shared Function GetNameValuesFromType(Instance As Object) As Dictionary(Of String, Object)
        Dim NameDic As New Dictionary(Of String, Object)
        Dim ScanTypes As Action(Of Object, String)
        ScanTypes = Sub(Inst As Object, LastLevelName As String)
                        If Inst Is Nothing Then Return
                        Dim tp = Inst.GetType
                        For Each m As FieldInfo In tp.GetFields
                            Dim Attrib = TryCast(GetCustomAttribute(m, GetType(FieldDisplayNameAttribute)), FieldDisplayNameAttribute)
                            Dim Val = m.GetValue(Inst)
                            Dim CurrentName As String = If(Attrib Is Nothing, m.Name, Attrib.Name)
                            If {"Empty", "MaxValue", "MinValue"}.Contains(CurrentName) OrElse Val Is Nothing Then
                                Continue For
                            End If
                            If Not NameDic.ContainsKey(LastLevelName + CurrentName) Then
                                NameDic.Add(LastLevelName + CurrentName, If(Val.ToString = Val.GetType.ToString, "(不能直接显示此数据)", Val))
                            End If
                            Dim DoNotDisplayMem = TryCast(GetCustomAttribute(m, GetType(DoNotDisplayMemberAttribute)), DoNotDisplayMemberAttribute)
                            If DoNotDisplayMem Is Nothing Then
                                If TypeOf Val IsNot String Then
                                    Dim Vals As IEnumerable
                                    Vals = TryCast(Val, IEnumerable)
                                    If Vals Is Nothing Then
                                        Dim Methods = From met In Val.GetType.GetMethods
                                        For Each mi In Methods
                                            If mi.Name = "ToArray" AndAlso mi.GetGenericArguments.Count = 0 AndAlso
                                            TypeOf mi.ReturnType.GetConstructor(Type.EmptyTypes).Invoke(Nothing) Is IEnumerable Then
                                                Vals = DirectCast(mi.Invoke(mi, Nothing), IEnumerable)
                                                Exit For
                                            End If
                                        Next
                                    End If
                                    If Vals IsNot Nothing Then
                                        Dim i As Integer = 0
                                        For Each v In Vals
                                            ScanTypes.Invoke(v, LastLevelName + CurrentName + $"({i}).")
                                            i += 1
                                        Next
                                    End If
                                End If
                            End If
                            ScanTypes(Val, LastLevelName + CurrentName + ".")
                        Next
                    End Sub
        ScanTypes.Invoke(Instance, String.Empty)
        Return NameDic
    End Function
    Sub New(Name As String)
        MyBase.New
        Me.Name = Name
    End Sub
End Class
