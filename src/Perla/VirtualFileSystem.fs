namespace Perla.VirtualFs

open System
open Zio
open Zio.FileSystems

open Perla
open Perla.Types
open Perla.Units
open Perla.Logger
open Perla.Plugins
open Perla.FileSystem

open FSharp.UMX
open FSharp.Control
open FSharp.Control.Reactive
open FsToolkit.ErrorHandling

open Fake.IO.Globbing.Operators
open System.Threading.Tasks

[<Struct>]
type ChangeKind =
  | Created
  | Deleted
  | Renamed
  | Changed

type FileChangedEvent =
  { serverPath: string<ServerUrl>
    userPath: string<UserPath>
    oldPath: string<SystemPath> option
    oldName: string<SystemPath> option
    changeType: ChangeKind
    path: string<SystemPath>
    name: string<SystemPath> }

[<Struct>]
type internal PathInfo =
  { globPath: string<SystemPath>
    localPath: string<UserPath>
    url: string<ServerUrl> }

type internal ApplyPluginsFn = string * string -> Async<FileTransform>

[<RequireQualifiedAccess>]
module VirtualFileSystem =

  let internal getGlobbedFiles path =
    !! $"{path}/**/*"
    -- $"{path}/**/bin/**"
    -- $"{path}/**/obj/**"
    -- $"{path}/**/*.fs"
    -- $"{path}/**/*.fsproj"

  let observablesForPath
    serverPath
    userPath
    path
    : IObservable<FileChangedEvent> =
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
          path = UMX.tag m.FullPath
          name = UMX.tag m.Name })

    let created =
      watcher.Created
      |> Observable.map (fun m ->
        { serverPath = serverPath
          userPath = userPath
          oldPath = None
          oldName = None
          changeType = Created
          path = UMX.tag m.FullPath
          name = UMX.tag m.Name })

    let deleted =
      watcher.Deleted
      |> Observable.map (fun m ->
        { serverPath = serverPath
          userPath = userPath
          oldPath = None
          oldName = None
          changeType = Created
          path = UMX.tag m.FullPath
          name = UMX.tag m.Name })

    let renamed =
      watcher.Renamed
      |> Observable.map (fun m ->
        { serverPath = serverPath
          userPath = userPath
          oldPath = Some(UMX.tag m.OldFullPath)
          oldName = Some(UMX.tag m.OldName)
          changeType = Renamed
          path = UMX.tag m.FullPath
          name = UMX.tag m.Name })

    [ changed; created; deleted; renamed ] |> Observable.mergeSeq

  let copyFileWithoutPlugins
    (pathInfo: PathInfo)
    (memoryFileSystem: IFileSystem)
    (physicalFileSystem: IFileSystem)
    =
    let globPath = UMX.untag pathInfo.globPath
    let localPath = UMX.untag pathInfo.localPath
    let url = UMX.untag pathInfo.url

    let targetPath =
      globPath.Replace(localPath, url)
      |> memoryFileSystem.ConvertPathFromInternal

    let targetDir = targetPath.GetDirectory()
    memoryFileSystem.CreateDirectory(targetDir)

    physicalFileSystem.CopyFileCross(
      globPath,
      memoryFileSystem,
      targetPath,
      true
    )

  let internal processFiles
    (injects: string list)
    (url: string<ServerUrl>)
    (userPath: string<UserPath>)
    (physicalFileSystem: IFileSystem)
    (memoryFileSystem: IFileSystem)
    (applyPlugins: string * string -> Async<FileTransform>)
    (globPath: string)
    =
    async {
      let globPath = globPath |> physicalFileSystem.ConvertPathFromInternal

      let localPath =
        UMX.untag userPath
        |> IO.Path.GetFullPath
        |> physicalFileSystem.ConvertPathFromInternal

      let pathInfo =
        { globPath = UMX.tag<SystemPath> globPath.FullName
          localPath = UMX.tag<UserPath> localPath.FullName
          url = url }

      let extension = globPath.GetExtensionWithDot()

      let isInFableMdules = globPath.FullName.Contains("fable_modules")

      if extension = ".js" && not isInFableMdules then
        let content = globPath.FullName |> physicalFileSystem.ReadAllText
        let injects = String.Join("\n", injects)
        physicalFileSystem.WriteAllText(globPath, $"{injects}\n{content}")

      if not (HasPluginsForExtension extension) || isInFableMdules then
        return
          copyFileWithoutPlugins pathInfo memoryFileSystem physicalFileSystem
      else
        let url = UMX.untag pathInfo.url
        let content = physicalFileSystem.ReadAllText globPath
        let! transform = applyPlugins (content, extension)

        let transform =
          if transform.extension = ".js" then
            let injects = String.Join("\n", injects)
            { transform with content = $"{injects}\n{transform.content}" }
          else
            transform

        let parentDir = UMX.untag url |> UPath

        let path =
          globPath
            .ChangeExtension(transform.extension)
            .FullName.Replace(localPath.FullName, parentDir.FullName)
          |> UPath

        memoryFileSystem.CreateDirectory(path.GetDirectory())

        return memoryFileSystem.WriteAllText(path, transform.content)
    }

  let internal mountDirectories
    (injects: string list)
    (applyPlugins: ApplyPluginsFn)
    (directories: Map<string<ServerUrl>, string<UserPath>>)
    (serverPaths: IFileSystem)
    (fs: IFileSystem)
    =
    async {
      let cwd = FileSystem.CurrentWorkingDirectory()

      for KeyValue(url, path) in directories do
        Logger.log ($"Mounting {path} into {url}...")

        do!
          IO.Path.Combine(UMX.untag cwd, UMX.untag path)
          |> IO.Path.GetFullPath
          |> getGlobbedFiles
          |> Seq.map (processFiles injects url path fs serverPaths applyPlugins)
          |> Async.Parallel
          |> Async.Ignore

      return ()
    }

  let internal tryCompileFile
    (readFile: string -> Task<string>)
    (event: FileChangedEvent)
    : Async<(FileChangedEvent * FileTransform) option> =
    asyncOption {
      let! file =
        task {
          try
            let! content = readFile (UMX.untag event.path)
            return Some content
          with ex ->
            Logger.log (
              $"[bold yellow]Could not process file {event.path}",
              ex = ex
            )

            return None
        }
        |> Async.AwaitTask

      let extension = IO.Path.GetExtension(UMX.untag event.path)

      if extension = ".js" then
        return event, { content = file; extension = ".js" }
      else
        let! transform = Plugins.ApplyPlugins(file, extension)

        return (event, transform)
    }

  let internal copyToDisk
    (
      tempDir: string<SystemPath>,
      mountedFileSystem: IFileSystem,
      physicalFileSystem: IFileSystem
    ) =
    let dir = UMX.untag tempDir
    let path = physicalFileSystem.ConvertPathFromInternal dir
    mountedFileSystem.CopyDirectory("/", physicalFileSystem, path, true, false)
    dir

  let internal updateInVirtualFs
    (injects: string list)
    (serverFs: IFileSystem)
    (event: FileChangedEvent, transform: Plugins.FileTransform)
    =
    let fullUserPath = UMX.untag event.userPath |> IO.Path.GetFullPath
    let injects = String.Join("\n", injects)

    let transform =
      { transform with content = $"{injects}\n{transform.content}" }

    let serverFullPath =
      let filePath = (UMX.untag event.path) |> IO.Path.GetFullPath

      filePath.Replace(fullUserPath, UMX.untag event.serverPath)
      |> serverFs.ConvertPathFromInternal

    serverFs.CreateDirectory(serverFullPath.GetDirectory())
    let updatadPath = serverFullPath.ChangeExtension(transform.extension)

    match event.changeType with
    | Created
    | Renamed
    | Changed -> serverFs.WriteAllText(updatadPath, transform.content)
    | Deleted -> ()

    event, transform


  let internal normalizeEventStream (stream, withFilter, withReadFile) =
    stream
    |> Observable.filter withFilter
    |> Observable.map (tryCompileFile withReadFile)
    |> Observable.switchAsync
    |> Observable.choose id

  let serverPaths = lazy (new MemoryFileSystem())

  let ApplyVirtualOperations (injects: string list) stream =
    let withReadFile path = IO.File.ReadAllTextAsync path
    let withFs = serverPaths.Value

    let withFilter event =
      event.path
      |> UMX.untag
      |> IO.Path.GetExtension
      |> (fun extension ->
        Plugins.HasPluginsForExtension extension || extension = ".js")

    let injects =
        [ for inject in injects do IO.File.ReadAllText(inject) ]

    normalizeEventStream (stream, withFilter, withReadFile)
    |> Observable.map (updateInVirtualFs injects withFs)

  let Mount config =
    async {
      use fs = new PhysicalFileSystem()
      let injects = config.esbuild.injects
      let injects =
        [ for inject in injects do IO.File.ReadAllText(inject) ]
      return!
        mountDirectories
          injects
          Plugins.ApplyPlugins
          (config.mountDirectories)
          serverPaths.Value
          fs
    }

  let CopyToDisk () =
    let tempDir = FileSystem.GetTempDir() |> UMX.tag<SystemPath>
    let withMount = serverPaths.Value
    use withFs = new PhysicalFileSystem()
    copyToDisk (tempDir, withMount, withFs)

  let GetFileChangeStream
    (mountedDirectories: Map<string<ServerUrl>, string<UserPath>>)
    =
    let cwd = FileSystem.CurrentWorkingDirectory() |> UMX.untag

    [ for KeyValue(url, path) in mountedDirectories ->
        IO.Path.Combine(cwd, UMX.untag path) |> observablesForPath url path ]
    |> Observable.mergeSeq
    |> Observable.filter (fun event ->
      (UMX.untag event.path).Contains(".fsproj")
      || (UMX.untag event.path).Contains("/bin/")
      || (UMX.untag event.path).Contains(".gitignore")
      || (UMX.untag event.path).Contains("/obj/")
      || (UMX.untag event.path).Contains(".fs")
      || (UMX.untag event.path).Contains(".fsx") |> not)
    |> Observable.throttle (TimeSpan.FromMilliseconds(450))

  let TryResolveFile (url: string<ServerUrl>) =
    try
      serverPaths.Value.ReadAllText $"{url}" |> Some
    with _ ->
      None
