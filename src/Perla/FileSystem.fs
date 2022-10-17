namespace Perla.FileSystem

open System
open System.IO
open System.IO.Compression
open System.Threading.Tasks

open FSharp.UMX

open Perla
open Perla.Units
open Perla.Json
open Perla.Logger
open Perla.PackageManager.Types

open CliWrap

open Flurl.Http

open ICSharpCode.SharpZipLib.GZip
open ICSharpCode.SharpZipLib.Tar
open FsToolkit.ErrorHandling

open Fake.IO.Globbing
open Fake.IO.Globbing.Operators

[<RequireQualifiedAccess>]
module FileSystem =

  module Operators =
    let inline (/) a b = Path.Combine(a, b)

  open Operators
  open Units

  let AssemblyRoot: string<SystemPath> =
    UMX.tag<SystemPath> AppContext.BaseDirectory

  let CurrentWorkingDirectory () : string<SystemPath> =
    UMX.tag<SystemPath> Environment.CurrentDirectory

  let Database: string<SystemPath> =
    (UMX.untag AssemblyRoot) / Constants.TemplatesDatabase |> UMX.tag

  let Templates: string<SystemPath> =
    (UMX.untag AssemblyRoot) / Constants.TemplatesDirectory |> UMX.tag

  let rec findConfigFile (directory: string, fileName: string) =
    let config = directory / fileName

    if File.Exists config then
      Some config
    else
      try
        let parent = Path.GetDirectoryName(config) |> Path.GetFullPath

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
    lazy (File.ReadAllText((UMX.untag AssemblyRoot) / "./livereload.js"))

  let WorkerScript =
    lazy (File.ReadAllText((UMX.untag AssemblyRoot) / "./worker.js"))

  let ensureFileContent<'T> (path: string<SystemPath>) (content: 'T) =
    try
      File.WriteAllBytes(UMX.untag path, content |> Json.ToBytes)
    with ex ->
      Logger.log (
        $"[bold red]Unable to write file at[/][bold yellow]{path}[/]",
        ex = ex,
        escape = false
      )

      exit (1)

    content

  let ExtractTemplateZip (name: string<SystemPath>) stream =
    let path = Path.Combine(UMX.untag Templates, UMX.untag name)

    try
      Directory.Delete(path)
    with _ ->
      ()

    Directory.CreateDirectory(path) |> ignore
    use zip = new ZipArchive(stream)
    zip.ExtractToDirectory path

  let RemoveTemplateDirectory (name: string<SystemPath>) =
    let path = Path.Combine(UMX.untag Templates, UMX.untag name)

    try
      Directory.Delete(path)
    with _ ->
      ()

  let EsbuildBinaryPath () : string<SystemPath> =
    let bin = if Env.IsWindows then "" else "bin"
    let exec = if Env.IsWindows then ".exe" else ""

    (UMX.untag AssemblyRoot) / "package" / bin / $"esbuild{exec}"
    |> Path.GetFullPath
    |> UMX.tag

  let chmodBinCmd () =
    Cli
      .Wrap("chmod")
      .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
      .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
      .WithArguments($"+x {EsbuildBinaryPath()}")

  let tryDownloadEsBuild (esbuildVersion: string) : Task<string option> =
    let binString = $"esbuild-{Env.PlatformString}-{Env.ArchString}"
    let compressedFile = (UMX.untag AssemblyRoot) / "esbuild.tgz"

    let url =
      $"https://registry.npmjs.org/{binString}/-/{binString}-{esbuildVersion}.tgz"

    compressedFile
    |> Path.GetDirectoryName
    |> Directory.CreateDirectory
    |> ignore

    task {
      try
        use! stream = url.GetStreamAsync()
        Logger.log $"Downloading esbuild from: {url}"

        use file = File.OpenWrite(compressedFile)

        do! stream.CopyToAsync file

        Logger.log $"Downloaded esbuild to: {file.Name}"

        return Some(file.Name)
      with ex ->
        Logger.log ($"Failed to download esbuild from: {url}", ex)
        return None
    }

  let decompressEsbuild (path: Task<string option>) =
    task {
      match! path with
      | Some path ->

        use stream = new GZipInputStream(File.OpenRead path)

        use archive =
          TarArchive.CreateInputTarArchive(stream, Text.Encoding.UTF8)

        path |> Path.GetDirectoryName |> archive.ExtractContents

        if Env.IsWindows |> not then
          Logger.log $"Executing: chmod +x on \"{EsbuildBinaryPath()}\""
          let res = chmodBinCmd().ExecuteAsync()
          do! res.Task :> Task

        Logger.log "Cleaning up!"

        File.Delete(path)

        Logger.log "This setup should happen once per machine"
        Logger.log "If you see it often please report a bug."
      | None -> ()

      return ()
    }

  let SetupEsbuild (esbuildVersion: string<Semver>) =
    Logger.log "Checking whether esbuild is present..."

    if File.Exists(EsbuildBinaryPath() |> UMX.untag) then
      Logger.log "esbuild is present."
      Task.FromResult(())
    else
      Logger.log "esbuild is not present, setting esbuild..."

      Logger.spinner (
        "esbuild is not present, setting esbuild...",
        fun context ->
          context.Status <- "Downloading esbuild..."

          tryDownloadEsBuild (UMX.untag esbuildVersion)
          |> (fun path ->
            context.Status <- "Extracting esbuild..."
            path)
          |> decompressEsbuild
          |> (fun path ->
            context.Status <- "Cleaning up extra files..."
            path)
      )

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

    DirectoryInfo(UMX.untag path)
      .EnumerateFiles("*.*", SearchOption.AllDirectories)
    |> Seq.filter (fun file -> file.Extension <> ".fsx")
    |> Seq.fold
         foldFilesAndTemplates
         (List.empty<FileInfo>, List.empty<FileInfo>)

type FileSystem =
  static member PerlaConfigText(?fromDirectory: string<SystemPath>) =

    let path = FileSystem.GetConfigPath Constants.PerlaConfigName fromDirectory

    try
      File.ReadAllText(UMX.untag path) |> Some
    with :? FileNotFoundException ->
      None

  static member SetCwdToPerlaRoot(?fromPath) =
    FileSystem.GetConfigPath Constants.PerlaConfigName fromPath
    |> UMX.untag
    |> Path.GetDirectoryName
    |> Path.GetFullPath
    |> Directory.SetCurrentDirectory

  static member ImportMap(?fromDirectory: string<SystemPath>) =
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

  static member PathForTemplate
    (
      name: string,
      branch: string,
      ?tplName: string
    ) =
    let tplName = defaultArg tplName ""

    Path.Combine(UMX.untag FileSystem.Templates, $"{name}-{branch}", tplName)

  static member GetTemplateScriptContent
    (
      name: string,
      branch: string,
      tplname: string
    ) =
    let readTemplateScript =
      let templateScriptPath = FileSystem.PathForTemplate(name, branch, tplname)

      try
        File.ReadAllText(
          Path.Combine(templateScriptPath, Constants.TemplatingScriptName)
        )
        |> Some
      with _ ->
        None

    let readRepoScript () =
      let repositoryScriptPath = FileSystem.PathForTemplate(name, branch)

      try
        File.ReadAllText(
          Path.Combine(repositoryScriptPath, Constants.TemplatingScriptName)
        )
        |> Some
      with _ ->
        None

    readTemplateScript |> Option.orElseWith (fun () -> readRepoScript ())

  static member WriteTplRepositoryToDisk
    (
      origin: string<SystemPath>,
      target: string<UserPath>,
      ?payload: obj
    ) =
    let originDirectory = UMX.cast origin |> Path.GetFullPath
    let targetDirectory = UMX.cast target |> Path.GetFullPath

    let (files, templates) = FileSystem.collectRepositoryFiles origin

    let compileFiles (payload: obj option) (file: string) =
      let tpl = Scriban.Template.Parse(file)
      tpl.Render(payload |> Option.toObj)

    let copyFiles () =
      files
      |> Array.ofList
      |> Array.Parallel.iter (fun file ->
        file.Directory.Create()
        let target = file.FullName.Replace(originDirectory, targetDirectory)
        file.CopyTo(target, true) |> ignore)

    let copyTemplates () =
      templates
      |> Array.ofList
      |> Array.Parallel.iter (fun file ->
        file.Directory.Create()

        let target =
          file
            .FullName
            .Replace(originDirectory, targetDirectory)
            .Replace(".tpl", "")

        let content = compileFiles payload (File.ReadAllText file.FullName)

        File.WriteAllText(target, content))

    DirectoryInfo(UMX.untag target).Create()
    copyFiles ()
    copyTemplates ()

  static member GenerateSimpleFable(path: string<SystemPath>) =
    let getDotnet () =
      let ext = if Env.IsWindows then ".exe" else ""
      Cli.Wrap($"dotnet{ext}")

    let newManifest () =
      getDotnet()
        .WithArguments("new tool-manifest")
        .WithValidation(CommandResultValidation.ZeroExitCode)
        .ExecuteAsync()

    let addFableToManifest () =
      getDotnet()
        .WithArguments("tool instal fable")
        .WithValidation(CommandResultValidation.ZeroExitCode)
        .ExecuteAsync()

    let createFableLib () =
      getDotnet()
        .WithArguments($"new classlib -lang F# -o {path} -n App")
        .WithValidation(CommandResultValidation.ZeroExitCode)
        .ExecuteAsync()

    async {
      try
        do! newManifest().Task |> Async.AwaitTask |> Async.Ignore
        do! addFableToManifest().Task |> Async.AwaitTask |> Async.Ignore
      with :? Exceptions.CommandExecutionException ->
        Logger.log
          "We could not add the Fable tool to the local tools manifest, you will have to do this yourself."

      do! createFableLib().Task |> Async.AwaitTask |> Async.Ignore
    }
