namespace Perla.Lib

open System
open System.Diagnostics
open System.IO
open System.Text

open FsToolkit.ErrorHandling

open Types


[<AutoOpen>]
module Extensions =

  type Path with

    static member PerlaRootDirectory =
      let assemblyLoc =
        Path.GetDirectoryName(Reflection.Assembly.GetEntryAssembly().Location)

      if String.IsNullOrWhiteSpace assemblyLoc then
        Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)
      else
        assemblyLoc

    static member LocalDBPath =
      Path.Combine(Path.PerlaRootDirectory, "templates.db")

    static member TemplatesDirectory =
      Path.Combine(Path.PerlaRootDirectory, "templates")

    static member GetPerlaConfigPath(?directoryPath: string) =
      let rec findConfigFile currDir =
        let path = Path.Combine(currDir, Constants.PerlaConfigName)

        if File.Exists path then
          Some path
        else
          match Path.GetDirectoryName currDir |> Option.ofObj with
          | Some parent ->
            if parent <> currDir then findConfigFile parent else None
          | None -> None

      let workDir = defaultArg directoryPath Environment.CurrentDirectory

      findConfigFile (Path.GetFullPath workDir)
      |> Option.defaultValue (Path.Combine(workDir, Constants.PerlaConfigName))

    static member GetProxyConfigPath(?directoryPath: string) =
      $"{defaultArg directoryPath (Environment.CurrentDirectory)}/{Constants.ProxyConfigName}"

    static member SetCurrentDirectoryToPerlaConfigDirectory() =
      Path.GetPerlaConfigPath()
      |> Path.GetDirectoryName
      |> Directory.SetCurrentDirectory



[<RequireQualifiedAccess>]
module Fs =
  open System.IO
  open FSharp.Control.Reactive

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


  ///<summary>
  /// Gets the base templates directory (next to the perla binary)
  /// and appends the final path repository name
  /// </summary>
  let getPerlaRepositoryPath (repositoryName: string) (branch: string) =
    Path.Combine(Path.TemplatesDirectory, $"{repositoryName}-{branch}")
    |> Path.GetFullPath

  let getPerlaTemplatePath
    (repo: PerlaTemplateRepository)
    (child: string option)
    =
    match child with
    | Some child -> Path.Combine(repo.path, child)
    | None -> repo.path
    |> Path.GetFullPath

  let getPerlaTemplateTarget projectName =
    Path.Combine("./", projectName) |> Path.GetFullPath

  let removePerlaRepository (repository: PerlaTemplateRepository) =
    Directory.Delete(repository.path, true)

  let getPerlaTemplateScriptContent templatePath clamRepoPath =
    let readTemplateScript =
      try
        File.ReadAllText(Path.Combine(templatePath, "templating.fsx")) |> Some
      with _ ->
        None

    let readRepoScript () =
      try
        File.ReadAllText(Path.Combine(clamRepoPath, "templating.fsx")) |> Some
      with _ ->
        None

    readTemplateScript |> Option.orElseWith (fun () -> readRepoScript ())

  let getPerlaRepositoryChildren (repo: PerlaTemplateRepository) =
    DirectoryInfo(repo.path).GetDirectories()

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

        return Json.FromBytes<PackagesLock> bytes
      with
      | :? System.IO.FileNotFoundException ->
        return
          { imports = Map.empty
            scopes = Map.empty }
      | ex -> return! ex |> Error
    }

  let writeLockFile configPath (fdsLock: PackagesLock) =
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
        return
          { imports = Map.empty
            scopes = Map.empty }
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
