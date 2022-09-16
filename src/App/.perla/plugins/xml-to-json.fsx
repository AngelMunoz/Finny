#i "nuget: C:/Users/scyth/repos/Perla/src/nuget"
#i "nuget: https://api.nuget.org/v3/index.json"
#r "nuget: Newtonsoft.Json, 12.0.3"
#r "nuget: CalceTypes, 1.0.1"

open System.IO
open System.Xml
open Newtonsoft.Json

open type System.Text.Encoding

open CalceTypes

let onTransform: OnTransformCallback =
    fun (args: TransformArgs) ->
        let doc = XmlDocument()
        doc.LoadXml(args.content)

        let content = JsonConvert.SerializeXmlNode doc |> UTF8.GetBytes

        { content = content |> UTF8.GetString }

let Plugin =
    { name = "xml-to-json"
      extension = ".fsproj"
      resolve = None
      load = None
      transform = Some onTransform }

Plugin
