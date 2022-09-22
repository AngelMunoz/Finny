#i "nuget: C:/Users/scyth/repos/Perla/src/nupkg"
#i "nuget: https://api.nuget.org/v3/index.json"
#r "nuget: Newtonsoft.Json, 12.0.3"
#r "nuget: CalceTypes, 1.0.2"

open System.Xml
open Newtonsoft.Json

open CalceTypes

let shouldTransform: OnShouldTransform =
    fun args ->
        [ ".xml"; ".fsproj"; ".csproj" ]
        |> List.contains args.extension.AsString 
        |> Ok

let transform: OnTransform =
    fun args ->
        let doc = XmlDocument()
        doc.LoadXml args.content

        try
            let content = JsonConvert.SerializeXmlNode(doc)

            { content = content
              targetExtension = FileExtension.Custom ".json" }
            |> Ok
        with ex ->
            PluginError.Transform ex.Message |> Error

let Plugin: PluginInfo =
    { name = "xml-to-json"
      pluginApi = PluginApi.Stable
      load = None
      shouldTransform = Some shouldTransform
      transform = Some transform
      injectImports = None }

Plugin
