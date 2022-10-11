namespace Perla.FileSystem

open System
open System.Diagnostics
open System.IO
open System.IO.Compression

open Perla
open Perla.Types
open Perla.Json
open Perla.Logger
open Perla.PackageManager.Types

open FsToolkit.ErrorHandling

[<RequireQualifiedAccess>]
module FileSystem =
  module Operators =
    let inline (/) a b = Path.Combine(a, b)

  open Operators

  let AssemblyRoot =
    let assemblyLoc =
      Path.GetDirectoryName(Reflection.Assembly.GetEntryAssembly().Location)

    if String.IsNullOrWhiteSpace assemblyLoc then
      Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)
    else
      assemblyLoc

  let CurrentWorkingDirectory () = Environment.CurrentDirectory

  let Database = Path.Combine(AssemblyRoot, Constants.TemplatesDatabase)

  let Templates = Path.Combine(AssemblyRoot, Constants.TemplatesDirectory)

  let rec FindConfigFile (directory: string, fileName: string) =
    let config = directory / fileName

    if File.Exists config then
      Some config
    else
      try
        let parent = Path.GetDirectoryName(config) |> Path.GetFullPath

        if parent <> directory then
          FindConfigFile(parent, fileName)
        else
          None
      with :? ArgumentNullException ->
        None

  let GetConfigPath (fileName: string) (fromDirectory: string option) =
    let workDir =
      match fromDirectory with
      | Some dir -> dir |> Path.GetFullPath
      | None -> CurrentWorkingDirectory()

    FindConfigFile(workDir, Constants.PerlaConfigName)
    |> Option.defaultValue (CurrentWorkingDirectory() / fileName)

  let PerlaConfigPath = GetConfigPath Constants.PerlaConfigName None

  let LiveReloadScript =
    lazy (File.ReadAllText(AssemblyRoot / "./livereload.js"))

  let WorkerScript = lazy (File.ReadAllText(AssemblyRoot / "./worker.js"))

  let ensureFileContent<'T> (path: string) (content: 'T) =
    try
      File.WriteAllBytes(path, content |> Json.ToBytes)
    with ex ->
      Logger.log (
        $"[bold red]Unable to write file at[/][bold yellow]{path}[/]",
        ex = ex,
        escape = false
      )

      exit (1)

    content

  let ExtractTemplateZip name stream =
    let path = Path.Combine(Templates, name)

    try
      Directory.Delete(path)
    with _ ->
      ()

    Directory.CreateDirectory(path) |> ignore
    use zip = new ZipArchive(stream)
    zip.ExtractToDirectory path

  let RemoveTemplateDirectory name =
    let path = Path.Combine(Templates, name)

    try
      Directory.Delete(path)
    with _ ->
      ()


  let EsbuildBinaryPath () =
    let bin = if Env.IsWindows then "" else "bin"
    let exec = if Env.IsWindows then ".exe" else ""

    AssemblyRoot / "package" / bin / $"esbuild{exec}" |> Path.GetFullPath

  let TplRepositoryChildTemplates path =
    try
      Directory.EnumerateDirectories path |> Seq.map (Path.GetDirectoryName)
    with :? DirectoryNotFoundException ->
      Logger.log
        $"Directories not found at {path}, this might be a bad template or a possible bug"

      Seq.empty

  let collectRepositoryFiles (path: string) =
    let foldFilesAndTemplates (files, templates) (next: FileInfo) =
      if next.FullName.Contains(".tpl.") then
        (files, next :: templates)
      else
        (next :: files, templates)

    let opts = EnumerationOptions()
    opts.RecurseSubdirectories <- true

    DirectoryInfo(path).EnumerateFiles("*.*", SearchOption.AllDirectories)
    |> Seq.filter (fun file -> file.Extension <> ".fsx")
    |> Seq.fold
         foldFilesAndTemplates
         (List.empty<FileInfo>, List.empty<FileInfo>)

type FileSystem =
  static member PerlaConfig(?fromDirectory: string) =
    let path = FileSystem.GetConfigPath Constants.PerlaConfigName fromDirectory

    try
      File.ReadAllBytes(path) |> Json.FromBytes
    with :? FileNotFoundException ->
      { PerlaConfig.DefaultConfig() with
          index = None
          fable = None
          build = None
          packages = None
          devServer =
            Some
              { DevServerConfig.DefaultConfig() with
                  autoStart = None
                  watchConfig = None
                  liveReload = None
                  useSSL = None
                  enableEnv = None
                  envPath = None } }
      |> FileSystem.ensureFileContent path

  static member SetCwdToPerlaRoot(?fromPath) =
    FileSystem.GetConfigPath Constants.PerlaConfigName fromPath
    |> Path.GetDirectoryName
    |> Path.GetFullPath
    |> Directory.SetCurrentDirectory

  static member ImportMap(?fromDirectory: string) =
    let path = FileSystem.GetConfigPath Constants.ImportMapName fromDirectory

    try
      File.ReadAllBytes(path) |> Json.FromBytes
    with :? FileNotFoundException ->
      { imports = Map.empty; scopes = None }
      |> FileSystem.ensureFileContent path

  static member ProxyConfig(?fromDirectory: string) =
    let path = FileSystem.GetConfigPath Constants.ProxyConfigName fromDirectory

    try
      File.ReadAllBytes(path) |> Json.FromBytes |> Some
    with :? FileNotFoundException ->
      None

  static member ReplacePerlaConfig(config: PerlaConfig, ?fromDirectory) =
    let path = FileSystem.GetConfigPath Constants.PerlaConfigName fromDirectory

    FileSystem.ensureFileContent path config

  static member WritePerlaConfigSection
    (
      section: PerlaConfigSection,
      ?fromDirectory
    ) =
    let path = FileSystem.GetConfigPath Constants.PerlaConfigName fromDirectory
    let config = Json.WritePerlaSection(section, File.ReadAllBytes path)
    FileSystem.ensureFileContent path config

  static member WritePerlaConfigSections
    (
      sections: PerlaConfigSection seq,
      ?fromDirectory
    ) =
    let path = FileSystem.GetConfigPath Constants.PerlaConfigName fromDirectory
    let config = Json.WritePerlaSections(sections, File.ReadAllBytes path)
    FileSystem.ensureFileContent path config

  static member WriteImportMap(map: ImportMap, ?fromDirectory) =
    let path = FileSystem.GetConfigPath Constants.ImportMapName fromDirectory
    FileSystem.ensureFileContent path map

  static member PathForTemplate(name, branch, ?tplName) =
    let tplName = defaultArg tplName ""

    Path.Combine(FileSystem.Templates, $"{name}-{branch}", tplName)

  static member GetTemplateScriptContent(name, branch, tplname) =
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

  static member WriteTplRepositoryToDisk(origin, target, ?payload: obj) =
    let originDirectory = Path.GetFullPath origin
    let targetDirectory = Path.GetFullPath target

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

    DirectoryInfo(target).Create()
    copyFiles ()
    copyTemplates ()
