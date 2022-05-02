open Calcetate
open System
open System.IO
open type System.Text.Encoding
open CalceTypes
open FsToolkit.ErrorHandling

let filesToProcess = 
    [
        { loader = "xml"
          source = Path.GetFullPath "./Calcetate.fsproj"
          url = Uri("/files/Calcetate.fsproj", UriKind.Relative) }
        { loader = "xml"
          // make it fail to see what happens 👀
          source = Path.GetFullPath "../CalceTypes/CalcTypes.fsproj"
          url = Uri("/files/CalceTypes.fsproj", UriKind.Relative) }
        { loader = "xml"
          // don't make it fail this time
          source = Path.GetFullPath "../CalceTypes/CalceTypes.fsproj"
          url = Uri("/files/CalceTypes.fsproj", UriKind.Relative) }
    ]
    
task {
    let tasks = 
        [ for file in filesToProcess do
            asyncOption {
                let! plugin =
                  Extensibility.LoadPluginFromScript("xml-to-json", Path.GetFullPath "./Plugin.fsx")
                let! onLoad = plugin.load
                try
                    return! onLoad file
                with _ ->
                    return! None
            }
            |> (fun result ->
                async {
                    match! result with 
                    | Some result -> return (Some result, file)
                    | None -> return (None, file)
                }
            )
        ]
    let! files = tasks |> Async.Sequential
    
    for file in files do
        match file with 
        | (Some file,_) ->
            printfn $"Output: {file.mimeType}, {UTF8.GetString(file.content).Substring(0, 100)}..."
        | (None, args) ->
            printfn $"We were not able to process this file %A{args.source |> Path.GetFileName}"
}
|> Async.AwaitTask
|> Async.RunSynchronously
