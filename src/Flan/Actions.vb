Imports System
Imports System.IO
Imports Flan.Types

Imports Microsoft.FSharp.Collections

Imports Perla.Logger
Imports Perla.PackageManager.Types
Imports PackageManager = Perla.PackageManager.PackageManager.PackageManager

Friend Module FileOperations
    Public Function TryGetFileContents(path As String) As Task(Of String)
        Try
            Return File.ReadAllTextAsync(path)
        Catch ex As FileNotFoundException
            Dim content As String = "{ ""imports"": {} }"
            Using _file = File.CreateText(path)
                _file.WriteLine(content)
            End Using
            Return Task.FromResult(content)
        End Try
    End Function

    Public Function WriteMapToOutput(output As String, content As String)
        Try
            File.WriteAllText(output, content)
            Return True
        Catch ex As Exception
            Logger.log("Failed to Write File", ex)
            Return False
        End Try
    End Function
End Module

Module Actions

    Public Async Function AddPackage(options As AddPackageOptions) As Task
        Dim _result = ImportMap.FromString(Await TryGetFileContents(options.Map))
        Dim _importMap As ImportMap
        If _result.IsOk Then
            _importMap = _result.ResultValue
        Else
            _importMap = ImportMap.CreateMap(New Dictionary(Of String, String)())
        End If
        Dim _envs = New GeneratorEnv() {GeneratorEnv.Module, GeneratorEnv.Browser}
        Try
            Dim __result =
                Await Logger.spinner(
                    $"Adding {options.Package}...",
                    PackageManager.AddJspm(options.Package, environments:=_envs, importMap:=_importMap)
                )
            If _result.IsOk Then
                _importMap = __result.ResultValue
            Else
                _importMap = ImportMap.CreateMap(New Dictionary(Of String, String)())
            End If
            WriteMapToOutput(options.Output, _importMap.ToJson())
        Catch ex As Exception
            Logger.log("Failed to Fetch dependency", ex)
            WriteMapToOutput(options.Output, _importMap.ToJson())
        End Try
    End Function

End Module
