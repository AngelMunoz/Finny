namespace Perla.VirtualFs

open System
open Zio
open Zio.FileSystems

open Perla
open Perla.Types
open Perla.Units
open Perla.FileSystem
open Perla.Extensibility

open FSharp.UMX
open FSharp.Control
open FSharp.Control.Reactive

open Fake.IO.Globbing
open Fake.IO.Globbing.Operators

[<Struct>]
type ChangeKind =
  | Created
  | Deleted
  | Renamed
  | Changed

type FileChangedEvent =
  { serverPath: string<ServerUrl>
    userPath: string<UserPath>
    oldPath: string option
    oldName: string option
    changeType: ChangeKind
    path: string
    name: string }

type IFileWatcher =
  inherit IDisposable
  abstract member FileChanged: IObservable<FileChangedEvent>

[<RequireQualifiedAccess>]
module VirtualFileSystem =
  let serverPaths = lazy (new MountFileSystem(new MemoryFileSystem(), true))

  let getGlobbedFiles path =
    !! $"{path}/**/*"
    -- $"{path}/**/bin/**"
    -- $"{path}/**/obj/**"
    -- $"{path}/**/*.fs"
    -- $"{path}/**/*.fsproj"

  let MountDirectories (directories: Map<string<ServerUrl>, string<UserPath>>) =
    let cwd = FileSystem.CurrentWorkingDirectory()

    async {
      for KeyValue (url, path) in directories do
        let memFs = new MemoryFileSystem()

        do!
          IO.Path.Combine(UMX.untag cwd, UMX.untag path)
          |> IO.Path.GetFullPath
          |> getGlobbedFiles
          |> Seq.map (fun globPath ->
            async {
              let! content =
                IO.File.ReadAllTextAsync globPath |> Async.AwaitTask

              let extension = IO.Path.GetExtension globPath

              let! transform = Plugins.ApplyPlugins(content, extension)
              let path = (UMX.untag path)[1..]

              let path =
                IO.Path.ChangeExtension(path, transform.extension)
                |> memFs.ConvertPathFromInternal

              memFs.WriteAllText(path, transform.content)
            })
          |> Async.Parallel
          |> Async.Ignore

        serverPaths.Value.Mount(UPath.op_Implicit (UMX.untag url), memFs)
    }

  let observablesForPath serverPath userPath path =
    let watcher =
      new IO.FileSystemWatcher(
        path,
        IncludeSubdirectories = true,
        NotifyFilter = (IO.NotifyFilters.FileName ||| IO.NotifyFilters.Size),
        EnableRaisingEvents = true
      )

    let changed =
      watcher.Changed
      |> Observable.map (fun m ->
        { serverPath = serverPath
          userPath = userPath
          oldPath = None
          oldName = None
          changeType = Changed
          path = m.FullPath
          name = m.Name })

    let created =
      watcher.Created
      |> Observable.map (fun m ->
        { serverPath = serverPath
          userPath = userPath
          oldPath = None
          oldName = None
          changeType = Created
          path = m.FullPath
          name = m.Name })

    let deleted =
      watcher.Deleted
      |> Observable.map (fun m ->
        { serverPath = serverPath
          userPath = userPath
          oldPath = None
          oldName = None
          changeType = Created
          path = m.FullPath
          name = m.Name })

    let renamed =
      watcher.Renamed
      |> Observable.map (fun m ->
        { serverPath = serverPath
          userPath = userPath
          oldPath = Some(m.OldFullPath)
          oldName = Some(m.OldName)
          changeType = Renamed
          path = m.FullPath
          name = m.Name })

    [ changed; created; deleted; renamed ] |> Observable.mergeSeq

  let GetFileChangeStream
    (mountedDirectories: Map<string<ServerUrl>, string<UserPath>>)
    =
    let cwd = FileSystem.CurrentWorkingDirectory() |> UMX.untag

    [ for KeyValue (url, path) in mountedDirectories ->
        IO.Path.Combine(cwd, UMX.untag path) |> observablesForPath url path ]
    |> Observable.mergeSeq
    |> Observable.filter (fun event ->
      event.path.Contains(".fsproj")
      || event.path.Contains("/bin/")
      || event.path.Contains(".gitignore")
      || event.path.Contains("/obj/")
      || event.path.Contains(".fs")
      || event.path.Contains(".fsx") |> not)
    |> Observable.throttle (TimeSpan.FromMilliseconds(450))

  let TryResolveFile (url: string<ServerUrl>) =
    try
      serverPaths.Value.ReadAllText $"{url}" |> Some
    with _ ->
      None

  let tryCompileFile (event: FileChangedEvent) =
    async {
      let! file =
        task {
          try
            let! content = IO.File.ReadAllTextAsync event.path
            return Some content
          with _ ->
            return None
        }
        |> Async.AwaitTask

      match file with
      | Some file ->
        let! transform =
          Plugins.ApplyPlugins(file, IO.Path.GetExtension event.path)

        return (event, Some transform)
      | None -> return (event, None)
    }

  let copyToDisk () =
    let dir = FileSystem.GetTempDir()
    let fs = new PhysicalFileSystem()
    let path = fs.ConvertPathFromInternal dir
    serverPaths.Value.CopyDirectory("/", fs, path, true, false)
    dir

  let updateInVirtualFs
    (
      event: FileChangedEvent,
      transform: Plugins.FileTransform option
    ) =
    let fullUserPath = UMX.untag event.userPath |> IO.Path.GetFullPath

    let serverFullPath =
      let filePath = event.path |> IO.Path.GetFullPath
      filePath.Replace(fullUserPath, UMX.untag event.serverPath)

    match event.changeType, transform with
    | Created, Some transform ->
      serverPaths.Value.CreateDirectory(
        serverFullPath |> IO.Path.GetDirectoryName |> UPath.op_Implicit
      )

      let updatadPath =
        IO.Path.ChangeExtension(serverFullPath, transform.extension)

      serverPaths.Value.WriteAllText(updatadPath, transform.content)
    | Renamed, Some transform
    | Changed, Some transform ->
      let updatadPath =
        IO.Path.ChangeExtension(serverFullPath, transform.extension)

      serverPaths.Value.WriteAllText(updatadPath, transform.content)
    | Deleted, _
    | Renamed, None
    | Created, None
    | Changed, None -> ()

    event, transform

  let ApplyVirtualOperations (stream: IObservable<FileChangedEvent>) =
    stream
    |> Observable.map tryCompileFile
    |> Observable.switchAsync
    |> Observable.map updateInVirtualFs

type VirtualFileSystem =
  static member Mount(config: PerlaConfig) =
    VirtualFileSystem.MountDirectories(config.mountDirectories)

  static member CopyToDisk() = VirtualFileSystem.copyToDisk ()
