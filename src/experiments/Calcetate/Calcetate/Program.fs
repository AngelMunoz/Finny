open System
open System.IO

open System.Text
open FSharp.Control
open FSharp.Control.Reactive

open Calcetate
open Calcetate.FileSystem
open Microsoft.FSharp.Core
open Perla.Lib
open Perla.Lib.Plugin
open Perla.Lib.Logger

open FsToolkit.ErrorHandling
open Zio

let perlaConfigPath = Path.GetPerlaConfigPath()

let perlaDir = Path.GetDirectoryName perlaConfigPath

let pluginsDir = getPluginsDir perlaDir

let plugins = loadPlugins pluginsDir

let config =
  Fs.getPerlaConfig perlaConfigPath
  |> Result.valueOr (fun _ -> failwith "failed")

let mountedDirectories = getMountedDirectories perlaDir config

mountDirectories mountedDirectories

let sourcesWatcher = getMountedDirsWatcher mountedDirectories

let applyPluginsToTransform =
  Extensibility.GetSupportedPlugins plugins |> Extensibility.ApplyPluginsToFile

let hasFileTransform (event: Fs.FileChangedEvent) =
  option {
    let! file =
      try
        mounted.ConvertPathFromInternal event.path
        |> mounted.GetFileEntry
        |> Some
      with ex ->
        Logger.log ("Unable to find file within mounted paths", ex)
        None

    let extension =
      (Path.GetExtension event.path).ToLowerInvariant()
      |> FileExtension.FromString

    return
      { originalPath = file.Path.FullName
        content = file.ReadAllText()
        extension = extension }
  }

let writeToDisk fileTransform =
  let output =
    Path.ChangeExtension(
      fileTransform.originalPath,
      fileTransform.extension.AsString
    )
    |> mounted.ConvertPathFromInternal

  use file =
    mounted.OpenFile(
      output,
      FileMode.OpenOrCreate,
      FileAccess.ReadWrite,
      FileShare.ReadWrite
    )

  fileTransform.content |> Encoding.UTF8.GetBytes |> file.Write
  Logger.log $"Writing file at: {output.FullName}"

sourcesWatcher
|> Observable.choose hasFileTransform
|> Observable.map applyPluginsToTransform
|> Observable.switchAsync
|> Observable.add writeToDisk

async {
  Logger.log "Starting Task"

  while true do
    let! line = Console.In.ReadLineAsync() |> Async.AwaitTask
    printfn "Got %s" line

    if line = "showfs" then
      mounted.EnumeratePaths("/src", "*.*", SearchOption.AllDirectories)
      |> Seq.iter (fun f -> printfn $"{f.FullName}")

    if line = "q" then
      printfn "good bye!"
      exit (0)
}
|> Async.RunSynchronously
