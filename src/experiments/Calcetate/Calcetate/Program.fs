open System
open Calcetate
open CalceTypes
open Calcetate.FileSystem
open Calcetate.Extensibility
open Perla.Lib
open Perla.Lib.Logger

open FsToolkit.ErrorHandling
open FSharp.Compiler.IO
open Zio
open Zio.FileSystems

let perlaConfigPath = System.IO.Path.GetPerlaConfigPath()
let perlaDir = System.IO.Path.GetDirectoryName perlaConfigPath

let pluginsDir = getPluginsDir perlaDir

let plugins = loadPlugins pluginsDir

let supportedExtensions = plugins |> List.map(fun p -> p.extension)

let config = Fs.getPerlaConfig perlaConfigPath |> Result.valueOr(fun _ -> failwith "failed")

mountDirectories perlaDir config

let watcher = getMountedWatcher()
watcher.EnableRaisingEvents <- true

let eventStream = watchEvents supportedExtensions watcher
eventStream
|> Observable.add(fun event ->
  let file = event.path
  let ext = System.IO.Path.GetExtension file
  let plugin = plugins |> List.find(fun p -> p.extension = ext)

  let file = mounted.ConvertPathFromInternal event.path |> mounted.GetFileEntry
  let content = file.ReadAllText()
  match plugin.transform  with
  | Some transform ->
    let result = transform { content = content; path = event.path }
    let output = mounted.ConvertPathFromInternal(event.path.Replace(ext, plugin.extension))
    use file = mounted.OpenFile(output, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite, System.IO.FileShare.ReadWrite)
    file.WriteAllText result.content
  | None -> ()
)

backgroundTask {
    Logger.log "Starting Task"

    while true do
        let! line = Console.In.ReadLineAsync()
        printfn "Got %s" line

        if line = "q" then
            printfn "good bye!"
            exit (0)
}
|> Async.AwaitTask
|> Async.RunSynchronously

