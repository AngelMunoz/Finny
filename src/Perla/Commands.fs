namespace Perla

open System
open FSharp.Control
open FsToolkit.ErrorHandling

open Types
open Server
open Build

open type Fs.Paths

module Commands =

  let startInteractive (configuration: FdsConfig) =
    let onStdinAsync = serverActions configuration

    let deServer =
      defaultArg configuration.devServer (DevServerConfig.DefaultConfig())

    let fableConfig =
      defaultArg configuration.fable (FableConfig.DefaultConfig())

    let autoStartServer = defaultArg deServer.autoStart true
    let autoStartFable = defaultArg fableConfig.autoStart true

    asyncSeq {
      if autoStartServer then "start"
      if autoStartFable then "start:fable"

      while true do
        let! value = Console.In.ReadLineAsync() |> Async.AwaitTask
        value
    }
    |> AsyncSeq.iterAsync onStdinAsync

  let startBuild (configuration: FdsConfig) = execBuild configuration


  let private (|ScopedPackage|Package|) (package: string) =
    if package.StartsWith("@") then
      ScopedPackage(package.Substring(1))
    else
      Package package

  let private parsePackageName (name: string) =

    let getVersion parts =

      let version =
        let version =
          parts |> Seq.tryLast |> Option.defaultValue ""

        if String.IsNullOrWhiteSpace version then
          None
        else
          Some version

      version

    match name with
    | ScopedPackage name ->
      // check if the user is looking to install a particular version
      // i.e. package@5.0.0
      if name.Contains("@") then
        let parts = name.Split("@")
        let version = getVersion parts

        $"@{parts.[0]}", version
      else
        $"@{name}", None
    | Package name ->
      if name.Contains("@") then
        let parts = name.Split("@")

        let version = getVersion parts
        parts.[0], version
      else
        name, None

  let runInit options =
    result {
      let path =
        match options.path with
        | Some path -> GetFdsConfigPath(path)
        | None -> GetFdsConfigPath()

      let config =
        FdsConfig.DefaultConfig(defaultArg options.withFable false)

      do! Fs.createFdsConfig path config

      return 0
    }

  let runSearch (options: SearchOptions) =
    taskResult {
      let! package =
        match options.package with
        | Some package -> Ok package
        | None -> Error PackageNotFoundException

      let! results = Http.searchPackage package options.page

      results.results
      |> Seq.truncate 5
      |> Seq.iter
           (fun package ->
             let maintainers =
               package.maintainers
               |> Seq.fold
                    (fun curr next -> $"{curr}{next.name} - {next.email}\n\t")
                    "\n\t"

             printfn "%s" ("".PadRight(10, '-'))

             printfn
               $"""name: {package.name}
Description: {package.description}
Maintainers:{maintainers}
Updated: {package.updatedAt.ToShortDateString()}"""

             printfn "%s" ("".PadRight(10, '-')))

      printfn $"Found: {results.meta.totalCount}"
      printfn $"Page {results.meta.page} of {results.meta.totalPages}"
      return 0
    }

  let runShow (options: ShowPackageOptions) =
    taskResult {
      let! package =
        match options.package with
        | Some package -> Ok package
        | None -> Error PackageNotFoundException

      let! package = Http.showPackage package

      let maintainers =
        package.maintainers
        |> Seq.rev
        |> Seq.truncate 5
        |> Seq.fold
             (fun curr next -> $"{curr}{next.name} - {next.email}\n\t")
             "\n\t"

      let versions =
        package.distTags
        |> Map.toSeq
        |> Seq.truncate 5
        |> Seq.fold
             (fun curr (name, version) -> $"{curr}{name} - {version}\n\t")
             "\n\t"

      printfn "%s" ("".PadRight(10, '-'))

      printfn
        $"""name: {package.name}
Description: {package.description}
Deprecated: %b{package.isDeprecated}
Dependency Count: {package.dependenciesCount}
License: {package.license}
Versions: {versions}
Maintainers:{maintainers}
Updated: {package.updatedAt.ToShortDateString()}"""

      printfn "%s" ("".PadRight(10, '-'))
      return 0
    }

  let runRemove (options: RemovePackageOptions) =
    taskResult {
      let name = defaultArg options.package ""

      if name = "" then
        return! PackageNotFoundException |> Error

      let! fdsConfig = Fs.getFdsConfig (GetFdsConfigPath())
      let! lockFile = Fs.getorCreateLockFile (GetFdsConfigPath())

      let deps =
        fdsConfig.packages
        |> Option.map (fun map -> map |> Map.remove name)

      let opts = { fdsConfig with packages = deps }

      let imports = lockFile.imports |> Map.remove name

      let scopes =
        lockFile.scopes
        |> Map.map (fun _ value -> value |> Map.remove name)

      do!
        Fs.writeLockFile
          (GetFdsConfigPath())
          { lockFile with
              scopes = scopes
              imports = imports }

      do! Fs.createFdsConfig (GetFdsConfigPath()) opts

      return 0
    }

  let runAdd (options: AddPackageOptions) =
    taskResult {
      let! package, version =
        match options.package with
        | Some package -> parsePackageName package |> Ok
        | None -> MissingPackageNameException |> Error

      let alias =
        options.alias |> Option.defaultValue package

      let source = defaultArg options.source Source.Skypack

      let version =
        match version with
        | Some version -> $"@{version}"
        | None -> ""

      let! (deps, scopes) =
        Http.getPackageUrlInfo $"{package}{version}" alias source

      let! fdsConfig = Fs.getFdsConfig (GetFdsConfigPath())
      let! lockFile = Fs.getorCreateLockFile (GetFdsConfigPath())

      let packages =
        fdsConfig.packages
        |> Option.defaultValue Map.empty
        |> Map.toList
        |> fun existing -> existing @ deps
        |> Map.ofList

      let fdsConfig =
        { fdsConfig with
            packages = packages |> Some }

      let lockFile =
        let imports =
          lockFile.imports
          |> Map.toList
          |> fun existing -> existing @ deps |> Map.ofList

        let scopes =
          lockFile.scopes
          |> Map.toList
          |> fun existing -> existing @ scopes |> Map.ofList

        { lockFile with
            imports = imports
            scopes = scopes }

      do! Fs.createFdsConfig (GetFdsConfigPath()) fdsConfig
      do! Fs.writeLockFile (GetFdsConfigPath()) lockFile

      return 0
    }
