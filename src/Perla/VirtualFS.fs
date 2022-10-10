namespace Perla.VirtualFs

open System
open System.Diagnostics
open System.IO
open System.IO.Compression

open Zio
open Zio.FileSystems

open Perla.Types
open Perla
open Perla.Logger
open Perla.PackageManager.Types


module Operators =
  let inline (/) (a: UPath) (b: UPath) = UPath.Combine(a, b)

/// <summary>
/// Encloses all of the operations related with operating with
/// source files for the user, reading files, mounting directories
/// resolving paths on disk, etc.
/// </summary>
/// <remarks>
/// At the moment this module doesn't handle any of the configuration files
/// already existing or handled by the <see cref="T:Perla.Lib.FS">Perla.Lib.Fs</see> module
/// </remarks>
[<RequireQualifiedAccess>]
module PerlaFs =
  open Operators
  open Scriban

  let disk = lazy (new PhysicalFileSystem())
  let mounted = lazy (new MountFileSystem(new MemoryFileSystem(), true))
  let PerlaFs = []

  let perlaRoot =
    lazy
      (let assemblyLoc =
        let assemblyLoc =
          Path.GetDirectoryName(Reflection.Assembly.GetEntryAssembly().Location)

        if String.IsNullOrWhiteSpace assemblyLoc then
          Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)
        else
          assemblyLoc

       assemblyLoc |> disk.Value.ConvertPathFromInternal)

  let workingDir =
    lazy (Environment.CurrentDirectory |> disk.Value.ConvertPathFromInternal)

  let rec findConfigFile (directory: UPath, fileName: UPath) =
    let config = directory / fileName

    if disk.Value.FileExists config then
      Some config
    else
      try
        let parent = config.GetDirectory()

        if parent <> directory then
          findConfigFile (parent, fileName)
        else
          None
      with :? ArgumentNullException ->
        None

  let AssemblyRoot =
    perlaRoot.Value.FullName |> disk.Value.ConvertPathToInternal

  let Database =
    (perlaRoot.Value / Constants.TemplatesDatabase).FullName
    |> disk.Value.ConvertPathToInternal

  let Templates =
    (perlaRoot.Value / Constants.TemplatesDirectory).FullName
    |> disk.Value.ConvertPathToInternal

  let CurrentWorkingDirectory =
    workingDir.Value |> disk.Value.ConvertPathToInternal

  let GetPerlaConfigPath (fromDirectory: string option) =

    let workDir =
      match fromDirectory with
      | Some dir -> dir |> disk.Value.ConvertPathFromInternal
      | None -> workingDir.Value

    findConfigFile (workDir, Constants.PerlaConfigName)
    |> Option.defaultValue (workingDir.Value / Constants.PerlaConfigName)
    |> disk.Value.ConvertPathToInternal

  let GetImportMapPath (fromDirectory: string option) =

    let workDir =
      match fromDirectory with
      | Some dir -> dir |> disk.Value.ConvertPathFromInternal
      | None -> workingDir.Value

    findConfigFile (workDir, Constants.ImportMapName)
    |> Option.defaultValue (workingDir.Value / Constants.ImportMapName)
    |> disk.Value.ConvertPathToInternal

  let GetProxyConfigPath (fromDirectory: string option) =
    let workDir =
      match fromDirectory with
      | Some dir -> dir |> disk.Value.ConvertPathFromInternal
      | None -> workingDir.Value

    findConfigFile (workDir, Constants.ProxyConfigName)
    |> Option.defaultValue (workingDir.Value / Constants.ProxyConfigName)
    |> disk.Value.ConvertPathToInternal

  let SetCwdToPerlaConfigRoot () =
    GetPerlaConfigPath None
    |> Path.GetDirectoryName
    |> Directory.SetCurrentDirectory

  let createTemplatesDirectory () =

    Templates
    |> disk.Value.ConvertPathFromInternal
    |> disk.Value.CreateDirectory

  let extractTemplateZip location stream =
    use zip = new ZipArchive(stream)
    let targetLocation = Directory.GetParent location
    let path = Path.Combine(Templates, targetLocation.Name)
    zip.ExtractToDirectory path

  let removeTemplateDir location =
    let targetLocation = Directory.GetParent location

    let path =
      (Templates |> disk.Value.ConvertPathFromInternal) / targetLocation.Name

    disk.Value.DeleteDirectory(path, true)

  let createConfig () =
    let config = PerlaConfig.DefaultConfig()

    { config with
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

  let collectRepositoryFiles (path: UPath) =
    let foldFilesAndTemplates (files, templates) (next: UPath) =
      if next.FullName.Contains(".tpl.") then
        (files, next :: templates)
      else
        (next :: files, templates)

    let opts = EnumerationOptions()
    opts.RecurseSubdirectories <- true

    disk.Value.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
    |> Seq.filter (fun path -> path.GetExtensionWithDot() <> ".fsx")
    |> Seq.fold foldFilesAndTemplates (List.empty<UPath>, List.empty<UPath>)

  let compileFiles (payload: obj option) (file: string) =
    let tpl = Template.Parse(file)
    tpl.Render(payload |> Option.toObj)



open Operators

type PerlaFs =

  static member Config() : PerlaConfig =
    let path =
      PerlaFs.GetPerlaConfigPath None
      |> PerlaFs.disk.Value.ConvertPathFromInternal

    match PerlaFs.disk.Value.TryGetFileSystemEntry path |> Option.ofObj with
    | Some config ->
      try
        PerlaFs.disk.Value.ReadAllBytes config.FullName |> Json.FromBytes
      with :? FileNotFoundException ->
        let defaultConfig = PerlaFs.createConfig ()
        PerlaFs.disk.Value.WriteAllText(path, defaultConfig |> Json.ToText)
        defaultConfig
    | None ->
      let defaultConfig = PerlaFs.createConfig ()
      PerlaFs.disk.Value.WriteAllText(path, defaultConfig |> Json.ToText)
      defaultConfig

  static member ImportMap() : ImportMap =
    let path =
      PerlaFs.GetImportMapPath None
      |> PerlaFs.disk.Value.ConvertPathFromInternal

    match PerlaFs.disk.Value.TryGetFileSystemEntry path |> Option.ofObj with
    | Some config ->
      try
        PerlaFs.disk.Value.ReadAllBytes config.FullName |> Json.FromBytes
      with :? FileNotFoundException ->
        let map = { imports = Map.empty; scopes = None }
        PerlaFs.disk.Value.WriteAllText(path, map |> Json.ToText)
        map
    | None ->
      let map = { imports = Map.empty; scopes = None }
      PerlaFs.disk.Value.WriteAllText(path, map |> Json.ToText)
      map

  static member WriteMap(map: ImportMap) =
    let path = PerlaFs.GetImportMapPath None

    PerlaFs.disk.Value.WriteAllText(path, map |> Json.ToText)

  static member GetPathForTemplate(name, branch, ?child) =
    let child = defaultArg child ""
    let tpls = PerlaFs.Templates |> PerlaFs.disk.Value.ConvertPathFromInternal
    let path = tpls / $"{name}-{branch}" / child
    path |> PerlaFs.disk.Value.ConvertPathToInternal

  static member TemplateOutput(name: string) =
    let cwd =
      PerlaFs.CurrentWorkingDirectory
      |> PerlaFs.disk.Value.ConvertPathFromInternal

    cwd / name |> PerlaFs.disk.Value.ConvertPathToInternal

  static member RemoveTemplateFromDisk(path: string) =
    let path = path |> PerlaFs.disk.Value.ConvertPathFromInternal

    PerlaFs.disk.Value.DeleteDirectory(path, true)

  static member GetTemplateScriptContent(templatePath, repositoryPath) =

    let readTemplateScript =
      try
        File.ReadAllText(Path.Combine(templatePath, "templating.fsx")) |> Some
      with _ ->
        None

    let readRepoScript () =
      try
        File.ReadAllText(Path.Combine(repositoryPath, "templating.fsx")) |> Some
      with _ ->
        None

    readTemplateScript |> Option.orElseWith (fun () -> readRepoScript ())

  static member GetTemplateChildren(path) =
    let path = path |> PerlaFs.disk.Value.ConvertPathFromInternal

    PerlaFs.disk.Value.EnumerateDirectories path
    |> Seq.map (fun f -> f |> PerlaFs.disk.Value.ConvertPathToInternal)

  static member WriteTemplateToDisk(origin, target, ?payload: obj) =
    let origin = PerlaFs.disk.Value.ConvertPathFromInternal origin
    let target = PerlaFs.disk.Value.ConvertPathFromInternal target

    let originName = origin.GetName()
    let targetName = origin.GetName()

    let (files, templates) = PerlaFs.collectRepositoryFiles origin

    let copyFiles () =
      files
      |> Array.ofList
      |> Array.Parallel.iter (fun file ->
        let target = file.FullName.Replace(originName, targetName)
        let dir = Path.GetDirectoryName(target)
        PerlaFs.disk.Value.CreateDirectory(dir)
        PerlaFs.disk.Value.CopyFile(file, target, true))

    let copyTemplates () =
      templates
      |> Array.ofList
      |> Array.Parallel.iter (fun path ->
        let target =
          path.FullName.Replace(originName, targetName).Replace(".tpl", "")

        let dir = Path.GetDirectoryName(targetName)
        PerlaFs.disk.Value.CreateDirectory(dir)

        let content =
          PerlaFs.disk.Value.ReadAllText path |> PerlaFs.compileFiles payload

        PerlaFs.disk.Value.WriteAllText(target, content))

    PerlaFs.disk.Value.CreateDirectory(target) |> ignore
    copyFiles ()
    copyTemplates ()
