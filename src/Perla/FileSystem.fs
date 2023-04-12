namespace Perla.FileSystem

open System
open System.IO
open System.IO.Compression
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices

open Spectre.Console

open CliWrap

open Flurl.Http

open ICSharpCode.SharpZipLib.GZip
open ICSharpCode.SharpZipLib.Tar

open FSharp.UMX

open FsToolkit.ErrorHandling

open FSharp.Control.Reactive

open Fake.IO.Globbing
open Fake.IO.Globbing.Operators

open Perla
open Perla.Units
open Perla.Json
open Perla.Logger
open Perla.PackageManager.Types
open Perla.Types

[<RequireQualifiedAccess>]
type PerlaFileChange =
  | Index
  | PerlaConfig
  | ImportMap



[<RequireQualifiedAccess>]
module FileSystem =

  module Operators =
    let inline (/) a b = Path.Combine(a, b)

  open Operators

  let AssemblyRoot: string<SystemPath> =
    UMX.tag<SystemPath> AppContext.BaseDirectory

  let CurrentWorkingDirectory () : string<SystemPath> =
    UMX.tag<SystemPath> Environment.CurrentDirectory

  let PerlaArtifactsRoot: string<SystemPath> =
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
    / Constants.ArtifactsDirectoryname
    |> UMX.tag<SystemPath>

  let Database: string<SystemPath> =
    (UMX.untag PerlaArtifactsRoot) / Constants.TemplatesDatabase |> UMX.tag

  let Templates: string<SystemPath> =
    (UMX.untag PerlaArtifactsRoot) / Constants.TemplatesDirectory |> UMX.tag

  let rec findConfigFile (directory: string, fileName: string) =
    let config = directory / fileName

    if File.Exists config then
      Some config
    else
      try
        let parent = Path.GetDirectoryName(directory) |> Path.GetFullPath

        if parent <> directory then
          findConfigFile (parent, fileName)
        else
          None
      with :? ArgumentNullException ->
        None

  let GetConfigPath
    (fileName: string)
    (fromDirectory: string<SystemPath> option)
    : string<SystemPath> =
    let workDir =
      match fromDirectory with
      | Some dir -> UMX.untag dir |> Path.GetFullPath |> UMX.tag
      | None -> CurrentWorkingDirectory() |> UMX.untag

    findConfigFile (workDir, UMX.tag fileName)
    |> Option.defaultValue ((CurrentWorkingDirectory() |> UMX.untag) / fileName)
    |> UMX.tag<SystemPath>

  let PerlaConfigPath: string<SystemPath> =
    GetConfigPath Constants.PerlaConfigName None

  let LiveReloadScript =
    lazy File.ReadAllText((UMX.untag AssemblyRoot) / "livereload.js")

  let WorkerScript =
    lazy File.ReadAllText((UMX.untag AssemblyRoot) / "worker.js")

  let TestingHelpersScript =
    lazy File.ReadAllText((UMX.untag AssemblyRoot) / "testing-helpers.js")

  let MochaRunnerScript =
    lazy File.ReadAllText((UMX.untag AssemblyRoot) / "mocha-runner.js")

  let DescriptionsFile =
    lazy
      (File.ReadAllBytes((UMX.untag AssemblyRoot) / "descriptions.json")
       |> Json.FromBytes<Map<string, string>>)

  let ensureFileContent<'T> (path: string<SystemPath>) (content: 'T) =
    try
      File.WriteAllBytes(UMX.untag path, content |> Json.ToBytes)
    with ex ->
      Logger.log (
        $"[bold red]Unable to write file at[/][bold yellow]{path}[/]",
        ex = ex,
        escape = false
      )

      exit 1

    content

  let ExtractTemplateZip
    (username: string, repository: string, branch: string)
    stream
    =
    let targetPath =
      Path.Combine(UMX.untag Templates, $"{username}-{repository}-{branch}")

    try
      Directory.Delete(targetPath, true)
    with ex ->
      ()

    use zip = new ZipArchive(stream)
    zip.ExtractToDirectory(UMX.untag Templates, true)

    Directory.Move(
      Path.Combine(UMX.untag Templates, $"{repository}-{branch}"),
      targetPath
    )

    let config =
      let config =
        try
          File.ReadAllText(targetPath) |> Some
        with e ->
          None

      match config with
      | Some config ->
        Thoth.Json.Net.Decode.fromString
          TemplateDecoders.TemplateConfigurationDecoder
          config
      | None -> Error "No Configuration File found"

    UMX.tag<SystemPath> targetPath, config

  let RemoveTemplateDirectory (path: string<SystemPath>) =
    try
      Directory.Delete(UMX.untag path)
    with ex ->
      Logger.log ($"There was an error removing: {path}", ex = ex)

  let EsbuildBinaryPath (version: string<Semver> option) : string<SystemPath> =
    let bin = if Env.IsWindows then "" else "bin"
    let exec = if Env.IsWindows then ".exe" else ""

    let version =
      match version with
      | Some version -> UMX.untag version
      | None -> Perla.Constants.Esbuild_Version

    (UMX.untag PerlaArtifactsRoot)
    / version
    / "package"
    / bin
    / $"esbuild{exec}"
    |> Path.GetFullPath
    |> UMX.tag

  let chmodBinCmd (esbuildVersion: string<Semver> option) =
    Cli
      .Wrap("chmod")
      .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
      .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
      .WithArguments($"+x {EsbuildBinaryPath(esbuildVersion)}")

  let tryDownloadEsBuild
    (
      esbuildVersion: string,
      cancellationToken: CancellationToken option
    ) : Task<string option> =
    let binString = $"{Env.PlatformString}-{Env.ArchString}"

    let compressedFile =
      (UMX.untag PerlaArtifactsRoot) / esbuildVersion / "esbuild.tgz"

    let url =
      $"https://registry.npmjs.org/@esbuild/{binString}/-/{binString}-{esbuildVersion}.tgz"

    compressedFile
    |> Path.GetDirectoryName
    |> Directory.CreateDirectory
    |> ignore

    task {
      try
        use! stream = url.GetStreamAsync(?cancellationToken = cancellationToken)
        Logger.log $"Downloading esbuild from: {url}"

        use file = File.OpenWrite(compressedFile)

        do!
          match cancellationToken with
          | Some token -> stream.CopyToAsync(file, cancellationToken = token)
          | None -> stream.CopyToAsync(file)

        Logger.log $"Downloaded esbuild to: {file.Name}"

        return Some(file.Name)
      with ex ->
        Logger.log ($"Failed to download esbuild from: {url}", ex)
        return None
    }

  let decompressEsbuild
    (esbuildVersion: string<Semver> option)
    (path: Task<string option>)
    =
    task {
      match! path with
      | Some path ->
        let extract () =
          use stream = new GZipInputStream(File.OpenRead path)

          use archive =
            TarArchive.CreateInputTarArchive(stream, Text.Encoding.UTF8)

          path |> Path.GetDirectoryName |> archive.ExtractContents

        extract ()

        if Env.IsWindows |> not then
          Logger.log
            $"Executing: chmod +x on \"{EsbuildBinaryPath esbuildVersion}\""

          let res = chmodBinCmd(esbuildVersion).ExecuteAsync()
          do! res.Task :> Task

        Logger.log "Cleaning up!"

        File.Delete(path)

        Logger.log "This setup should happen once per machine"
        Logger.log "If you see it often please report a bug."
      | None -> ()

      return ()
    }

  let TryReadTsConfig () =
    let path = Path.Combine($"{CurrentWorkingDirectory()}", "tsconfig.json")

    try
      File.ReadAllText path |> Some
    with _ ->
      None

  let GetTempDir () =
    let tmp = Path.GetTempPath()
    let path = Path.Combine(tmp, Guid.NewGuid().ToString())
    Directory.CreateDirectory(path) |> ignore
    path |> Path.GetFullPath

  let TplRepositoryChildTemplates
    (path: string<SystemPath>)
    : string<SystemPath> seq =
    try
      UMX.untag path
      |> Directory.EnumerateDirectories
      |> Seq.map (Path.GetDirectoryName >> UMX.tag<SystemPath>)
    with :? DirectoryNotFoundException ->
      Logger.log
        $"Directories not found at {path}, this might be a bad template or a possible bug"

      Seq.empty

  let collectRepositoryFiles (path: string<SystemPath>) =
    let foldFilesAndTemplates (files, templates) (next: FileInfo) =
      if next.FullName.Contains(".tpl.") then
        (files, next :: templates)
      else
        (next :: files, templates)

    let opts = EnumerationOptions()
    opts.RecurseSubdirectories <- true

    try
      DirectoryInfo(UMX.untag path)
        .EnumerateFiles("*.*", SearchOption.AllDirectories)
      |> Seq.filter (fun file -> file.Extension <> ".fsx")
      |> Seq.fold
        foldFilesAndTemplates
        (List.empty<FileInfo>, List.empty<FileInfo>)
    with :? DirectoryNotFoundException ->
      Logger.log (
        "[bold red]While the repository was found, the chosen template was not[/]",
        escape = false
      )

      Logger.log (
        $"Please ensure you chose the correct template and [bold red]{DirectoryInfo(UMX.untag path).Name}[/] exists",
        escape = false
      )

      (List.empty, List.empty)

  let getPerlaFilesMonitor () =
    let fsw =
      new FileSystemWatcher(
        UMX.untag PerlaConfigPath |> Path.GetDirectoryName |> Path.GetFullPath,
        "*",
        IncludeSubdirectories = false,
        NotifyFilter =
          (NotifyFilters.Size
           ||| NotifyFilters.LastWrite
           ||| NotifyFilters.FileName),
        EnableRaisingEvents = true
      )

    fsw.Filters.Add(".html")
    fsw.Filters.Add(".jsonc")
    fsw

type FileSystem =

  static member PerlaConfigText
    ([<Optional>] ?fromDirectory: string<SystemPath>)
    =

    let path = FileSystem.GetConfigPath Constants.PerlaConfigName fromDirectory

    try
      File.ReadAllText(UMX.untag path) |> Some
    with :? FileNotFoundException ->
      None

  static member SetCwdToPerlaRoot([<Optional>] ?fromPath) =
    FileSystem.GetConfigPath Constants.PerlaConfigName fromPath
    |> UMX.untag
    |> Path.GetDirectoryName
    |> Path.GetFullPath
    |> Directory.SetCurrentDirectory

  static member GetImportMap([<Optional>] ?fromDirectory: string<SystemPath>) =
    let path = FileSystem.GetConfigPath Constants.ImportMapName fromDirectory

    try
      File.ReadAllBytes(UMX.untag path) |> Json.FromBytes
    with :? FileNotFoundException ->
      { imports = Map.empty; scopes = None }
      |> FileSystem.ensureFileContent path

  static member IndexFile(fromConfig: string<SystemPath>) =
    File.ReadAllText(UMX.untag fromConfig)

  static member PluginFiles() =
    let path =
      Path.Combine(
        UMX.untag (FileSystem.CurrentWorkingDirectory()),
        ".perla",
        "plugins"
      )

    !! $"{path}/**/*.fsx"
    |> Seq.toArray
    |> Array.Parallel.map (fun path -> path, File.ReadAllText(path))


  static member WriteImportMap(map: ImportMap, ?fromDirectory) =
    let path = FileSystem.GetConfigPath Constants.ImportMapName fromDirectory
    FileSystem.ensureFileContent path map

  static member WritePerlaConfig(?config: JsonObject, ?fromDirectory) =
    let path = FileSystem.GetConfigPath Constants.PerlaConfigName fromDirectory

    match config with
    | Some config ->
      try
        File.WriteAllText(
          UMX.untag path,
          config.ToJsonString(DefaultJsonOptions())
        )
      with ex ->
        Logger.log (
          $"[bold red]Unable to write file at[/][bold yellow]{path}[/]",
          ex = ex,
          escape = false
        )

        exit 1
    | None -> ()


  static member ObservePerlaFiles
    (
      indexPath: string,
      [<Optional>] ?cancellationToken: CancellationToken
    ) =
    let notifier = FileSystem.getPerlaFilesMonitor ()

    match cancellationToken with
    | Some cancel ->
      cancel.UnsafeRegister((fun _ -> notifier.Dispose()), ()) |> ignore
    | None -> ()

    [ notifier.Changed :> IObservable<FileSystemEventArgs>; notifier.Created ]
    |> Observable.mergeSeq
    |> Observable.throttle (TimeSpan.FromMilliseconds(400))
    |> Observable.filter (fun event ->
      indexPath.ToLowerInvariant().Contains(event.Name)
      || event.Name.Contains(Constants.PerlaConfigName)
      || event.Name.Contains(Constants.ImportMapName))
    |> Observable.map (fun event ->
      match event.Name.ToLowerInvariant() with
      | value when value.Contains(Constants.PerlaConfigName) ->
        PerlaFileChange.PerlaConfig
      | value when value.Contains(Constants.ImportMapName) ->
        PerlaFileChange.ImportMap
      | _ -> PerlaFileChange.Index)

  static member SetupEsbuild
    (
      esbuildVersion: string<Semver>,
      [<Optional>] ?cancellationToken: CancellationToken
    ) =
    Logger.log "Checking whether esbuild is present..."

    if
      File.Exists(
        FileSystem.EsbuildBinaryPath(Some esbuildVersion) |> UMX.untag
      )
    then
      Logger.log "esbuild is present."
      Task.FromResult(())
    else
      Logger.log "esbuild is not present, setting esbuild..."

      Logger.spinner (
        "esbuild is not present, setting esbuild...",
        fun context ->
          context.Status <- "Downloading esbuild..."

          FileSystem.tryDownloadEsBuild (
            UMX.untag esbuildVersion,
            cancellationToken
          )
          |> (fun path ->
            context.Status <- "Extracting esbuild..."
            path)
          |> FileSystem.decompressEsbuild (Some esbuildVersion)
          |> (fun path ->
            context.Status <- "Cleaning up extra files..."
            path)
      )


  static member WriteTplRepositoryToDisk
    (
      origin: string<SystemPath>,
      target: string<UserPath>,
      ?payload: obj
    ) =
    let originDirectory = UMX.cast origin |> Path.GetFullPath
    let targetDirectory = UMX.cast target |> Path.GetFullPath

    let files, templates = FileSystem.collectRepositoryFiles origin


    let copyFiles (ctx: ProgressTask) =
      files
      |> Array.ofList
      |> Array.Parallel.iter (fun file ->
        file.Directory.Create()
        let target = file.FullName.Replace(originDirectory, targetDirectory)
        Directory.CreateDirectory(Path.GetDirectoryName target) |> ignore
        file.CopyTo(target, true) |> ignore
        ctx.Increment(1.))

    let copyTemplates (ctx: ProgressTask) =
      let compileFiles (payload: obj option) (file: string) =
        let tpl = Scriban.Template.Parse(file)
        tpl.Render(payload |> Option.toObj)

      templates
      |> Array.ofList
      |> Array.Parallel.iter (fun file ->
        file.Directory.Create()

        let target =
          file.FullName
            .Replace(originDirectory, targetDirectory)
            .Replace(".tpl", "")

        let content = compileFiles payload (File.ReadAllText file.FullName)

        File.WriteAllText(target, content)
        ctx.Increment(1.))

    DirectoryInfo(UMX.untag target).Create()
    let progress = AnsiConsole.Progress()

    progress.Start(fun ctx ->
      let copyTask =
        ctx.AddTask(
          "Copy Files",
          ProgressTaskSettings(AutoStart = true, MaxValue = files.Length)
        )


      copyFiles copyTask
      copyTask.StopTask()

      if templates.Length > 0 then
        let processTemplates =
          ctx.AddTask(
            "Process Templates",
            ProgressTaskSettings(AutoStart = true, MaxValue = templates.Length)
          )

        copyTemplates processTemplates
        processTemplates.StopTask())

  static member GetDotEnvFilePaths(?fromDirectory) =
    let path =
      FileSystem.GetConfigPath Constants.PerlaConfigName fromDirectory
      |> UMX.untag
      |> Path.GetDirectoryName

    !! $"{path}/*.env" ++ $"{path}/*.*.env" |> Seq.map UMX.tag<SystemPath>
