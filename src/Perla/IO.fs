namespace Perla


open System
open System.Collections
open System.Text
open FsToolkit.ErrorHandling

open Perla
open Perla.Types
open Perla.Logger

[<RequireQualifiedAccess>]
module Env =
  open System.Runtime.InteropServices

  let isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)

  let platformString =
    if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
      "windows"
    else if RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
      "linux"
    else if RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
      "darwin"
    else if RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD) then
      "freebsd"
    else
      failwith "Unsupported OS"

  let archString =
    match RuntimeInformation.OSArchitecture with
    | Architecture.Arm -> "arm"
    | Architecture.Arm64 -> "arm64"
    | Architecture.X64 -> "64"
    | Architecture.X86 -> "32"
    | _ -> failwith "Unsupported Architecture"

  let getPerlaEnvVars () =
    let env = Environment.GetEnvironmentVariables()
    let prefix = "PERLA_"

    [ for entry in env do
        let entry = entry :?> DictionaryEntry
        let key = entry.Key :?> string
        let value = entry.Value :?> string

        if key.StartsWith(prefix) then
          (key.Replace(prefix, String.Empty), value) ]

[<RequireQualifiedAccess>]
module Json =
  open System.Text.Json
  open System.Text.Json.Serialization

  let private jsonOptions () =
    JsonSerializerOptions(
      WriteIndented = true,
      AllowTrailingCommas = true,
      ReadCommentHandling = JsonCommentHandling.Skip,
      UnknownTypeHandling = JsonUnknownTypeHandling.JsonElement,
      DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    )

  let ToBytes value =
    JsonSerializer.SerializeToUtf8Bytes(value, jsonOptions ())

  let FromBytes<'T> (bytes: byte array) =
    JsonSerializer.Deserialize<'T>(ReadOnlySpan bytes, jsonOptions ())

  let ToText value =
    JsonSerializer.Serialize(value, jsonOptions ())

  let ToTextMinified value =
    let opts = jsonOptions ()
    opts.WriteIndented <- false
    JsonSerializer.Serialize(value, opts)

  let ToPackageJson dependencies =
    JsonSerializer.Serialize({| dependencies = dependencies |}, jsonOptions ())


[<RequireQualifiedAccess>]
module Fs =
  open System.IO
  open FSharp.Control.Reactive
  open Perla.PackageManager.Types

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

  let getPerlaEnvContent () =
    option {
      let env = Env.getPerlaEnvVars ()
      let sb = StringBuilder()

      for key, value in env do
        sb.Append($"""export const {key} = "{value}";""") |> ignore

      let content = sb.ToString()

      if String.IsNullOrWhiteSpace content then
        return! None
      else
        return content
    }


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
    let fsw =
      new FileSystemWatcher(Path.GetPerlaConfigPath() |> Path.GetDirectoryName)

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
