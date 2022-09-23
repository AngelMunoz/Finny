open System.IO
open FSharp.Control.Reactive
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

let perlaConfigPath =
  Path.GetPerlaConfigPath()

let perlaDir =
  Path.GetDirectoryName perlaConfigPath

let pluginsDir = getPluginsDir perlaDir

let plugins = loadPlugins pluginsDir

let config =
  Fs.getPerlaConfig perlaConfigPath
  |> Result.valueOr (fun _ -> failwith "failed")

let mountedDirectories =
  getMountedDirectories perlaDir config

mountDirectories mountedDirectories

let eventStream =
  getMountedDirsWatcher mountedDirectories

eventStream
|> Observable.filter (fun f -> mounted.FileExists f.path)
|> Observable.add (fun event ->
  printfn "%A" event
  let ext = (Path.GetExtension event.path).ToLowerInvariant()

  let file =
    try
      mounted.ConvertPathFromInternal event.path |> mounted.GetFileEntry
      |> Some
    with ex ->
      printfn "%s" ex.Message
      None

  let content =
    match file with
    | Some file -> file.ReadAllText()
    | None -> ""

  let onShouldTransform (plugin: PluginInfo) =
    match plugin.shouldTransform with
    | Some shouldTransform ->
      let result =
        shouldTransform
          { content = content
            runtime = Runtime.DevServer
            extension = FileExtension.FromString ext }

      match result with
      | Ok value -> value
      | Error err -> false
    | None -> false

  let onTransform (plugin: PluginInfo) =
    match plugin.transform with
    | Some transform ->
      try
        let result =
          transform
            { runtime = Runtime.DevServer
              content = content
              currentExtension = FileExtension.Custom ext }

        match result with
        | Ok result ->
          let output =
            mounted.ConvertPathFromInternal(
              Path.ChangeExtension(
                event.path,
                result.targetExtension.AsString
              )
            )

          use file =
            mounted.OpenFile(
              output,
              FileMode.OpenOrCreate,
              FileAccess.ReadWrite,
              FileShare.ReadWrite
            )

          file.WriteAllText result.content
          printfn "Writing file at: %s" output.FullName
        | Error err -> eprintfn "%A" err
      with ex ->
        eprintfn "%O" ex
    | None -> printfn $"No plugins for '{ext}' files were found"

  plugins
  |> List.tryFind onShouldTransform
  |> Option.map onTransform
  |> Option.defaultValue ())

async {
  Logger.log "Starting Task"

  while true do
    let! line = Console.In.ReadLineAsync() |> Async.AwaitTask
    printfn "Got %s" line

    if line = "showfs" then
        mounted.EnumeratePaths("/src", "*.*", SearchOption.AllDirectories)
        |> Seq.iter(fun f -> printfn $"{f.FullName}")

    if line = "q" then
      printfn "good bye!"
      exit (0)
}
|> Async.RunSynchronously
