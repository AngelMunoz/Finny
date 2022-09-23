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

let watchEvents
  mountPoint
  (watcher: IFileSystemWatcher)
  : IObservable<Fs.FileChangedEvent> =
  let throttle = TimeSpan.FromMilliseconds(400.)

  let getMountPath eventPath = $"{mountPoint}{eventPath}"

  let changed event =
    event
    |> Observable.throttle throttle
    |> Observable.map (fun (event: FileChangedEventArgs) ->
      let changed: Fs.FileChangedEvent =
        { oldName = None
          ChangeType = Fs.ChangeKind.Created
          path = getMountPath event.FullPath.FullName
          name = event.Name }

      changed)

  let renamed event =
    event
    |> Observable.throttle throttle
    |> Observable.map (fun (event: FileRenamedEventArgs) ->
      let renamed: Fs.FileChangedEvent =
        { oldName = getMountPath event.OldFullPath.FullName |> Some
          ChangeType = Fs.ChangeKind.Created
          path = getMountPath event.FullPath.FullName
          name = event.Name }

      renamed)

  [ changed watcher.Changed
    changed watcher.Created
    changed watcher.Deleted
    renamed watcher.Renamed ]
  |> Observable.mergeSeq

let getMountedDirectories projectRoot (config: PerlaConfig) =
  lazy
    (let mountedDirs =
      config.devServer
      |> Option.map (fun f -> f.mountDirectories)
      |> Option.flatten
      |> Option.defaultValue Map.empty

     let path = fs.ConvertPathFromInternal(projectRoot)

     [ for KeyValue (key, value) in mountedDirs do
         value,
         fs.GetOrCreateSubFileSystem(UPath.Combine(path, key)) :> IFileSystem ])

let mountDirectories (mountedDirs: Lazy<(string * IFileSystem) list>) =
  for mountPoint, subdir in mountedDirs.Value do
    let mfs = new MemoryFileSystem()
    subdir.CopyDirectory("/", mfs, "/", true)
    mounted.Mount(mountPoint, mfs)

let getMountedDirsWatcher (mountedDirs: Lazy<(string * IFileSystem) list>) =
  [ for mountPoint, filesystem in mountedDirs.Value do
      let watcher = filesystem.Watch("/")
      watcher.IncludeSubdirectories <- true

      watcher.NotifyFilter <-
        NotifyFilters.Size
        ||| NotifyFilters.LastWrite
        ||| NotifyFilters.FileName

      watcher.EnableRaisingEvents <- true
      watchEvents mountPoint watcher ]
  |> Observable.mergeSeq

let getPluginsDir projectRoot =
  let path = fs.ConvertPathFromInternal(projectRoot)

  let target = UPath.Combine(path, ".perla", "plugins")

  fs.GetOrCreateSubFileSystem(target)

let loadPlugins (fs: SubFileSystem) =
  let plugins =
    fs.EnumerateFileEntries("/", "*.fsx", SearchOption.AllDirectories)

  plugins
  |> Seq.map (fun file ->
    let content =
      file.ReadAllText()
      |> Option.ofObj
      |> Option.map (fun f ->
        if String.IsNullOrWhiteSpace f then None else Some f)
      |> Option.flatten

    Extensibility.LoadPluginFromScript(file.Name, content))
  |> Seq.choose id
  |> Seq.toList
