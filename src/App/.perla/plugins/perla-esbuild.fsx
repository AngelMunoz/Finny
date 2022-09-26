#i "nuget: C:/Users/scyth/repos/Perla/src/nupkg"
#i "nuget: https://api.nuget.org/v3/index.json"
#r "nuget: Perla.Lib, 0.24.1"
#r "nuget: CalceTypes, 0.0.7"

open System.Xml
open CalceTypes


let shouldTransform: FilePredicate =
    fun args -> [ ".xml"; ".fsproj"; ".csproj" ] |> List.contains args.extension.AsString

let transform: Transform =
    fun args ->
        let doc = XmlDocument()

        try
            doc.LoadXml args.content

            { args with
                content = JsonConvert.SerializeXmlNode(doc)
                extension = FileExtension.Custom ".json" }
        with ex ->
            eprintfn "Failed to Transform content: %s" ex.Message
            args

plugin "xml-to-json" {
    should_process_file shouldTransform
    with_transform transform
}
