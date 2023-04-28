namespace Perla.VirtualFs

open System
open Zio
open Zio.FileSystems

open Perla
open Perla.Types
open Perla.Units
open Perla.Logger
open Perla.Plugins
open Perla.Extensibility
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

type FileChangedEvent = {
  serverPath: string<ServerUrl>
  userPath: string<UserPath>
  oldPath: string<SystemPath> option
  oldName: string<SystemPath> option
  changeType: ChangeKind
  path: string<SystemPath>
  name: string<SystemPath>
}

[<Struct>]
type internal PathInfo = {
  globPath: string<SystemPath>
  localPath: string<UserPath>
  url: string<ServerUrl>
}

type internal ApplyPluginsFn = FileTransform -> Async<FileTransform>




[<RequireQualifiedAccess>]
module VirtualFileSystem =
  [<return: Struct>]
  let (|IsFSharpSource|_|) (value: string) =
    let isBin = value.Contains("/bin/")
    let isObj = value.Contains("/obj/")
    let isFsproj = value.EndsWith(".fsproj")
    let isFSharp = value.EndsWith(".fs")
    let isFSharpScript = value.EndsWith(".fsx")

    if isBin || isObj || isFsproj || isFSharp || isFSharpScript then
      ValueSome()
    else
      ValueNone

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
      let filters =
        (IO.NotifyFilters.FileName
         ||| IO.NotifyFilters.Size
         ||| IO.NotifyFilters.LastWrite
         ||| IO.NotifyFilters.CreationTime
         ||| IO.NotifyFilters.DirectoryName)

      new IO.FileSystemWatcher(
        IO.Path.GetFullPath(path),
        IncludeSubdirectories = true,
        NotifyFilter = filters,
        EnableRaisingEvents = true
      )

    let changed =
      watcher.Changed
      |> Observable.map (fun m -> {
        serverPath = serverPath
        userPath = userPath
        oldPath = None
        oldName = None
        changeType = Changed
        path = UMX.tag m.FullPath
        name = UMX.tag m.Name
      })

    let created =
      watcher.Created
      |> Observable.map (fun m -> {
        serverPath = serverPath
        userPath = userPath
        oldPath = None
        oldName = None
        changeType = Created
        path = UMX.tag m.FullPath
        name = UMX.tag m.Name
      })

    let deleted =
      watcher.Deleted
      |> Observable.map (fun m -> {
        serverPath = serverPath
        userPath = userPath
        oldPath = Some(UMX.tag m.FullPath)
        oldName = Some(UMX.tag m.Name)
        changeType = Deleted
        path = UMX.tag m.FullPath
        name = UMX.tag m.Name
      })

    let renamed =
      watcher.Renamed
      |> Observable.map (fun m -> {
        serverPath = serverPath
        userPath = userPath
        oldPath = Some(UMX.tag m.OldFullPath)
        oldName = Some(UMX.tag m.OldName)
        changeType = Renamed
        path = UMX.tag m.FullPath
        name = UMX.tag m.Name
      })

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
    (plugins: string list)
    (url: string<ServerUrl>)
    (userPath: string<UserPath>)
    (physicalFileSystem: IFileSystem)
    (memoryFileSystem: IFileSystem)
    (applyPlugins: ApplyPluginsFn)
    (globPath: string)
    =
    async {
      let globPath = globPath |> physicalFileSystem.ConvertPathFromInternal

      let localPath =
        UMX.untag userPath
        |> IO.Path.GetFullPath
        |> physicalFileSystem.ConvertPathFromInternal

      let pathInfo = {
        globPath = UMX.tag<SystemPath> globPath.FullName
        localPath = UMX.tag<UserPath> localPath.FullName
        url = url
      }

      let extension = globPath.GetExtensionWithDot()

      let isInFableMdules = globPath.FullName.Contains("fable_modules")

      let hasPluginForExtension =
        PluginRegistry.HasPluginsForExtension plugins extension

      if not hasPluginForExtension || isInFableMdules then
        return
          copyFileWithoutPlugins pathInfo memoryFileSystem physicalFileSystem
      else
        let url = UMX.untag pathInfo.url
        let content = physicalFileSystem.ReadAllText globPath

        let! transform =
          applyPlugins {
            content = content
            extension = extension
          }

        let parentDir = UMX.untag url |> UPath

        let path =
          globPath
            .ChangeExtension(transform.extension)
            .FullName.Replace(localPath.FullName, parentDir.FullName)
          |> UPath

        memoryFileSystem.CreateDirectory(path.GetDirectory())

        return memoryFileSystem.WriteAllText(path, transform.content)
    }

  let tryReadFile
    (readFile: string -> Task<string option>)
    (event: FileChangedEvent)
    =
    taskOption {
      try
        let! content = readFile (UMX.untag event.path)

        return
          event,
          {
            content = content
            extension = IO.Path.GetExtension(UMX.untag event.path)
          }
      with ex ->
        Logger.log (
          $"[bold yellow]Could not process file {event.path}",
          ex = ex
        )

        return! None
    }

  let tryCompileFile
    (applyPlugins: ApplyPluginsFn)
    (readFileResult: (FileChangedEvent * FileTransform) option)
    : Async<(FileChangedEvent * FileTransform) option> =
    asyncOption {
      let! (event, file) = readFileResult
      let! transform = applyPlugins file

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
    (serverFs: IFileSystem)
    (event: FileChangedEvent, transform: FileTransform)
    =
    let fullUserPath = UMX.untag event.userPath |> IO.Path.GetFullPath

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
    | Deleted ->
      match serverFs.TryGetFileSystemEntry(updatadPath) |> Option.ofObj with
      | Some entry -> entry.Delete()
      | None -> ()

    event, transform

  let serverPaths = lazy (new MemoryFileSystem())

  let ApplyVirtualOperations plugins stream =
    let withReadFile path = taskOption {
      try
        return! IO.File.ReadAllTextAsync path
      with ex ->
        return! None
    }

    let withFs = serverPaths.Value

    let withFilter event =
      event.path
      |> UMX.untag
      |> IO.Path.GetExtension
      |> PluginRegistry.HasPluginsForExtension plugins

    stream
    |> Observable.filter withFilter
    |> Observable.map (tryReadFile withReadFile)
    |> Observable.switchTask
    |> Observable.map (tryCompileFile (PluginRegistry.ApplyPlugins plugins))
    |> Observable.switchAsync
    |> Observable.choose id
    |> Observable.map (updateInVirtualFs withFs)

  let Mount config = async {
    use fs = new PhysicalFileSystem()

    let cwd = FileSystem.CurrentWorkingDirectory()
    let applyPlugins = PluginRegistry.ApplyPlugins config.plugins
    let serverPaths = serverPaths.Value

    for KeyValue(url, path) in config.mountDirectories do
      Logger.log ($"Mounting {path} into {url}...")

      do!
        IO.Path.Combine(UMX.untag cwd, UMX.untag path)
        |> IO.Path.GetFullPath
        |> getGlobbedFiles
        |> Seq.map (
          processFiles config.plugins url path fs serverPaths applyPlugins
        )
        |> Async.Parallel
        |> Async.Ignore
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

    [
      for KeyValue(url, path) in mountedDirectories ->
        IO.Path.Combine(cwd, UMX.untag path) |> observablesForPath url path
    ]
    |> Observable.mergeSeq
    |> Observable.filter (fun event ->
      match UMX.untag event.path with
      | IsFSharpSource() -> false
      | _ -> true)
    |> Observable.throttle (TimeSpan.FromMilliseconds(450))

  let TryResolveFile (url: string<ServerUrl>) =
    try
      serverPaths.Value.ReadAllBytes $"{url}" |> Some
    with _ ->
      None
