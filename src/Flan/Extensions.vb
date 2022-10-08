Imports System.Runtime.CompilerServices
Imports Microsoft.FSharp.Core

<Extension>
Module Extensions

    <Extension>
    Public Function ToOption(Of T)(value As T) As FSharpOption(Of T)
        If value Is Nothing Then
            Return FSharpOption(Of T).None
        Else
            Return FSharpOption(Of T).Some(value)
        End If
    End Function

    <Extension>
    Public Function ToValueOption(Of T)(value As T) As FSharpValueOption(Of T)
        If value Is Nothing Then
            Return FSharpValueOption(Of T).None
        Else
            Return FSharpValueOption(Of T).Some(value)
        End If
    End Function

    <Extension>
    Public Function TryGetValue(Of T)(value As FSharpOption(Of T), ByRef outValue As T)
        Try
            outValue = value.Value
            Return True
        Catch ex As NullReferenceException
            Return False
        End Try
    End Function
    <Extension>
    Public Function TryGetValue(Of T)(value As FSharpValueOption(Of T), ByRef outValue As T)
        Try
            outValue = value.Value
            Return True
        Catch ex As InvalidOperationException
            Return False
        End Try
    End Function

End Module
