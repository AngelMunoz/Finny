namespace Perla


open System
open System.IO
open System.Text

open FsToolkit.ErrorHandling

open Perla
open Perla.Types
open Perla.Json
open Perla.FileSystem
open Perla.Logger
open Perla.PackageManager.Types

[<RequireQualifiedAccess>]
module Fs =
  open FSharp.Control.Reactive

  type WatchResource =
    | File of string
    | Directory of string

  type ChangeKind =
    | Created
    | Deleted
    | Renamed
    | Changed

  type FileChangedEvent =
    { oldName: string option
      ChangeType: ChangeKind
      path: string
      name: string }

  type IFileWatcher =
    inherit IDisposable
    abstract member FileChanged: IObservable<FileChangedEvent>

  let getPerlaConfig filepath =
    try
      let bytes = File.ReadAllBytes filepath
      Json.FromBytes<PerlaConfig> bytes |> Ok
    with ex ->
      ex |> Error

  let getProxyConfig filepath =
    try
      let bytes = File.ReadAllBytes filepath
      Json.FromBytes<Map<string, string>> bytes |> Some
    with ex ->
      None

  let ensureParentDirectory path =
    try
      Directory.CreateDirectory(path) |> ignore |> Ok
    with ex ->
      ex |> Error

  let createPerlaConfig path config =
    let serialized = Json.ToBytes config

    try
      File.WriteAllBytes(path, serialized) |> Ok
    with ex ->
      Error ex

  let getOrCreateLockFile configPath =
    taskResult {
      try
        let path = Path.GetFullPath($"%s{configPath}.importmap")

        do! ensureParentDirectory (Path.GetDirectoryName(path))

        let bytes = File.ReadAllBytes(path)

        return Json.FromBytes<ImportMap> bytes
      with
      | :? System.IO.FileNotFoundException ->
        return { imports = Map.empty; scopes = None }
      | ex -> return! ex |> Error
    }

  let writeLockFile configPath (fdsLock: ImportMap) =
    let path = Path.GetFullPath($"%s{configPath}.importmap")
    let serialized = Json.ToBytes fdsLock

    try
      File.WriteAllBytes(path, serialized) |> Ok
    with ex ->
      Error ex

  let getOrCreateImportMap path =
    taskResult {
      try
        let path = Path.GetFullPath(path)

        do! ensureParentDirectory (Path.GetDirectoryName(path))

        let bytes = File.ReadAllBytes(path)

        return Json.FromBytes<ImportMap> bytes
      with
      | :? System.IO.FileNotFoundException ->
        return { imports = Map.empty; scopes = None }
      | ex -> return! ex |> Error
    }

  let writeImportMap path importMap =
    result {
      let bytes = Json.ToBytes importMap

      try
        let path = Path.GetFullPath(path)

        Directory.CreateDirectory(Path.GetDirectoryName(path)) |> ignore

        File.WriteAllBytes(path, bytes)
      with ex ->
        return! ex |> Error
    }

  let CompileErrWatcherEvent = lazy (Event<string>())

  let PublishCompileErr err =
    CompileErrWatcherEvent.Value.Trigger err

  let compileErrWatcher () = CompileErrWatcherEvent.Value.Publish


  let getPerlaConfigWatcher () =
    let fsw = new FileSystemWatcher(FileSystem.PerlaConfigPath)

    fsw.NotifyFilter <-
      NotifyFilters.FileName ||| NotifyFilters.Size ||| NotifyFilters.LastWrite

    fsw.Filters.Add Constants.PerlaConfigName
    fsw.IncludeSubdirectories <- false
    fsw.EnableRaisingEvents <- true
    fsw

  let getFileWatcher (config: WatchConfig) =

    let getWatcher resource =
      let fsw =
        match resource with
        | Directory path ->
          let fsw = new FileSystemWatcher(path)
          fsw.IncludeSubdirectories <- true

          let filters =
            defaultArg
              config.extensions
              [ "*.js"; "*.css"; "*.ts"; "*.tsx"; "*.jsx"; "*.json" ]

          for filter in filters do
            fsw.Filters.Add(filter)

          fsw
        | File path ->
          let fsw =
            new FileSystemWatcher(
              path |> Path.GetFullPath |> Path.GetDirectoryName
            )

          fsw.IncludeSubdirectories <- false
          fsw.Filters.Add(Path.GetFileName path)
          fsw.EnableRaisingEvents <- true
          fsw

      fsw.NotifyFilter <- NotifyFilters.FileName ||| NotifyFilters.Size
      fsw.EnableRaisingEvents <- true
      fsw

    let watchers =

      (defaultArg config.directories [ "./index.html"; "./src" ])
      |> Seq.map (fun dir ->
        (if Path.GetExtension(dir) |> String.IsNullOrEmpty then
           Directory dir
         else
           File dir)
        |> getWatcher)


    let subs =
      watchers
      |> Seq.map (fun watcher ->
        [ watcher.Renamed
          |> Observable.throttle (TimeSpan.FromMilliseconds(400.))
          |> Observable.map (fun e ->
            { oldName = Some e.OldName
              ChangeType = Renamed
              name = e.Name
              path = e.FullPath })
          watcher.Changed
          |> Observable.throttle (TimeSpan.FromMilliseconds(400.))
          |> Observable.map (fun e ->
            { oldName = None
              ChangeType = Changed
              name = e.Name
              path = e.FullPath })
          watcher.Deleted
          |> Observable.throttle (TimeSpan.FromMilliseconds(400.))
          |> Observable.map (fun e ->
            { oldName = None
              ChangeType = Deleted
              name = e.Name
              path = e.FullPath })
          watcher.Created
          |> Observable.throttle (TimeSpan.FromMilliseconds(400.))
          |> Observable.map (fun e ->
            { oldName = None
              ChangeType = Created
              name = e.Name
              path = e.FullPath }) ]
        |> Observable.mergeSeq)

    { new IFileWatcher with
        override _.Dispose() : unit =
          watchers |> Seq.iter (fun watcher -> watcher.Dispose())

        override _.FileChanged: IObservable<FileChangedEvent> =
          Observable.mergeSeq subs }

  let private tryReadFileWithExtension file ext =
    taskResult {
      try
        match ext with
        | LoaderType.Typescript ->
          let! content = File.ReadAllTextAsync($"{file}.ts")
          return (content, LoaderType.Typescript)
        | LoaderType.Jsx ->
          let! content = File.ReadAllTextAsync($"{file}.jsx")
          return (content, LoaderType.Jsx)
        | LoaderType.Tsx ->
          let! content = File.ReadAllTextAsync($"{file}.tsx")
          return (content, LoaderType.Tsx)
      with ex ->
        return! ex |> Error
    }

  let tryReadFile (filepath: string) =
    let fileNoExt =
      Path.Combine(
        Path.GetDirectoryName(filepath),
        Path.GetFileNameWithoutExtension(filepath)
      )

    tryReadFileWithExtension fileNoExt LoaderType.Typescript
    |> TaskResult.orElseWith (fun _ ->
      tryReadFileWithExtension fileNoExt LoaderType.Jsx)
    |> TaskResult.orElseWith (fun _ ->
      tryReadFileWithExtension fileNoExt LoaderType.Tsx)

  let tryGetTsconfigFile () =
    try
      File.ReadAllText("./tsconfig.json") |> Some
    with _ ->
      None
