Imports System
Imports System.IO
Imports AngleSharp
Imports AngleSharp.Html
Imports AngleSharp.Html.Parser
Imports Perla
Imports Perla.FileSystem
Imports Spectre.Console

Public Enum FileKind As Integer
    Html
    Cshtml

End Enum

Module Actions
    Public Sub CreatePerlaFile()
        Dim fileContent = $"{{
  ""$schema"": ""{Constants.JsonSchemaUrl}"",
  ""dependencies"": []
}}
"
        Dim filePath = Path.Combine(".", Constants.PerlaConfigName)
        File.WriteAllText(filePath, fileContent)
    End Sub

    Public Function GetImportMapContent(kind As FileKind)
        Dim mapContent = FileSystem.GetImportMap().ToJson()

        If kind = FileKind.Cshtml Then
            mapContent = mapContent.Replace("@", "@@")
        End If
        Return mapContent
    End Function

    Public Sub AddToLayout(file As FileInfo)
        AnsiConsole.MarkupLineInterpolated($"Setting Import map to: [green]'{file.Name}'[/]")
        Using browserCtx As New BrowsingContext()
            Dim fileContents = file.OpenRead()

            Dim doc = browserCtx.GetService(Of IHtmlParser).ParseDocument(fileContents)
            fileContents.Dispose()

            Dim fKind = If(file.Extension.ToLowerInvariant() = ".cshtml", FileKind.Cshtml, FileKind.Html)
            Dim mapContent = GetImportMapContent(fKind)

            Dim existing = doc.QuerySelector("script[type=importmap]")

            Dim formatter = New PrettyMarkupFormatter() With {
                .Indentation = "  "
            }

            If existing Is Nothing Then

                AnsiConsole.MarkupLine("[yellow]Import map not found in the specified file[/], adding a new one!")
                Dim map = doc.CreateElement("script")
                map.SetAttribute("type", "importmap")
                map.InnerHtml = mapContent
                doc.Head.Append(map)
                IO.File.WriteAllText(file.FullName, doc.ToHtml(formatter))

                Return
            End If

            AnsiConsole.MarkupLineInterpolated($"[yellow]There is an existing import map[/], [orange1]replacing contents[/]")
            existing.InnerHtml = mapContent
            IO.File.WriteAllText(file.FullName, doc.ToHtml(formatter))
        End Using
    End Sub

End Module

