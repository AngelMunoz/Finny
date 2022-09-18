module Calcetate.FileSystem

open FSharp.Control.Reactive

open System
open System.IO

open Zio
open Zio.FileSystems

open Perla.Lib
open Perla.Lib.Types

let fs = new PhysicalFileSystem()
let mounted = new MountFileSystem(new MemoryFileSystem(), true)

let mountDirectories projectRoot (config: PerlaConfig) =
  let path = fs.ConvertPathFromInternal(projectRoot)

  let mountedDirs =
    config.devServer
    |> Option.map (fun f -> f.mountDirectories)
    |> Option.flatten
    |> Option.defaultValue Map.empty

  for KeyValue (key, value) in mountedDirs do
    let target = UPath.Combine(path, key)
    mounted.Mount(value, fs.GetOrCreateSubFileSystem(target))

let getPluginsDir projectRoot =
  let path = fs.ConvertPathFromInternal(projectRoot)
  let target = UPath.Combine(path, ".perla", "plugins")

  fs.GetOrCreateSubFileSystem(target)

let getMountedWatcher () =
  let watcher = mounted.Watch("/")
  watcher.IncludeSubdirectories <- true

  watcher.NotifyFilter <-
    NotifyFilters.Size
    ||| NotifyFilters.LastWrite
    ||| NotifyFilters.FileName

  watcher

let watchEvents
  (supportedExtensions: string list)
  (watcher: IFileSystemWatcher)
  : IObservable<Fs.FileChangedEvent> =
  let throttle = TimeSpan.FromMilliseconds(400.)

  let changed event =
    event
    |> Observable.throttle throttle
    |> Observable.map (fun (event: FileChangedEventArgs) ->
      let changed: Fs.FileChangedEvent =
        { oldName = None
          ChangeType = Fs.ChangeKind.Created
          path = event.FullPath.FullName
          name = event.Name }

      changed)

  let renamed event =
    event
    |> Observable.throttle throttle
    |> Observable.map (fun (event: FileRenamedEventArgs) ->
      let renamed: Fs.FileChangedEvent =
        { oldName = event.OldFullPath.FullName |> Some
          ChangeType = Fs.ChangeKind.Created
          path = event.FullPath.FullName
          name = event.Name }

      renamed)

  [ changed watcher.Changed
    changed watcher.Created
    changed watcher.Deleted
    renamed watcher.Renamed ]
  |> Observable.mergeSeq
  |> Observable.filter (fun event ->
    let path = fs.ConvertPathFromInternal event.path

    supportedExtensions
    |> List.contains (path.GetExtensionWithDot()))


let loadPlugins (fs: SubFileSystem) =
  let plugins =
    fs.EnumerateFileEntries("/", "*.fsx", SearchOption.AllDirectories)

  plugins
  |> Seq.map (fun file ->
    let content =
      file.ReadAllText()
      |> Option.ofObj
      |> Option.map (fun f ->
        if String.IsNullOrWhiteSpace f then
          None
        else
          Some f)
      |> Option.flatten

    Extensibility.LoadPluginFromScript(file.Name, content))
  |> Seq.choose id
  |> Seq.toList
