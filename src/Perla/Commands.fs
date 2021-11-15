namespace Perla

open System
open System.Text.Json
open FSharp.Control
open FsToolkit.ErrorHandling

open Types
open Server
open Build

open Argu

open type Fs.Paths


type ServerArgs =
  | [<AltCommandLine("-a")>] Auto_Start of bool option
  | [<AltCommandLine("-p")>] Port of int option
  | [<AltCommandLine("-h")>] Host of string option
  | [<AltCommandLine("-s")>] Use_Ssl of bool option

  interface IArgParserTemplate with
    member this.Usage =
      match this with
      | Port _ -> "Select the server port, defaults to 7331"
      | Host _ -> "Server host, defaults to localhost"
      | Use_Ssl _ -> "Forces the requests to go through HTTPS. Defaults to true"
      | Auto_Start _ -> "Starts the server without action required by the user."

type BuildArgs =
  | [<AltCommandLine("-i")>] Index_File of string option
  | [<AltCommandLine("-ev")>] Esbuild_Version of string option
  | [<AltCommandLine("-o")>] Out_Dir of string option
  interface IArgParserTemplate with
    member this.Usage =
      match this with
      | Index_File _ ->
        "The Entry File for the web application. Defaults to \"index.html\""
      | Esbuild_Version _ ->
        "Use a specific esbuild version. defaults to \"0.12.9\""
      | Out_Dir _ -> "Where to output the files. Defaults to \"./dist\""

type InitArgs =
  | [<AltCommandLine("-p")>] Path of string
  | [<AltCommandLine("-wf")>] With_Fable of bool option

  static member ToOptions(args: ParseResults<InitArgs>) : InitOptions =
    { path = args.TryGetResult(Path)
      withFable = args.TryGetResult(With_Fable) |> Option.flatten }

  interface IArgParserTemplate with
    member this.Usage: string =
      match this with
      | Path _ -> "Where to write the config file"
      | With_Fable _ -> "Include fable options in the config file"

type SearchArgs =
  | [<AltCommandLine("-n")>] Name of string
  | [<AltCommandLine("-p")>] Page of int option

  static member ToOptions(args: ParseResults<SearchArgs>) : SearchOptions =
    { package = args.TryGetResult(Name)
      page = args.TryGetResult(Page) |> Option.flatten }

  interface IArgParserTemplate with
    member this.Usage: string =
      match this with
      | Name _ -> "The name of the package to search for."
      | Page _ -> "Page number to search at."

type ShowArgs =
  | [<AltCommandLine("-p")>] Package of string

  static member ToOptions(args: ParseResults<ShowArgs>) : ShowPackageOptions =
    { package = args.TryGetResult(Package) }

  interface IArgParserTemplate with
    member this.Usage: string =
      match this with
      | Package _ -> "The name of the package to show information about."

type RemoveArgs =
  | [<AltCommandLine("-p")>] Package of string

  static member ToOptions
    (args: ParseResults<RemoveArgs>)
    : RemovePackageOptions =
    { package = args.TryGetResult(Package) }

  interface IArgParserTemplate with
    member this.Usage: string =
      match this with
      | Package _ ->
        "The name of the package to remove from the import map this can also be aliased name."

type AddArgs =
  | [<AltCommandLine("-p")>] Package of string
  | [<AltCommandLine("-a")>] Alias of string option
  | [<AltCommandLine("-s")>] Source of Source option

  static member ToOptions(args: ParseResults<AddArgs>) : AddPackageOptions =
    { package = args.TryGetResult(Package)
      alias = args.TryGetResult(Alias) |> Option.flatten
      source = args.TryGetResult(Source) |> Option.flatten }

  interface IArgParserTemplate with
    member this.Usage: string =
      match this with
      | Package _ -> "The name of the package to show information about."
      | Alias _ -> "Specifier for this particular module."
      | Source _ ->
        "The name of the source you want to install a package from. e.g. unpkg or skypack."

type ListArgs =
  | As_Package_Json

  static member ToOptions(args: ParseResults<ListArgs>) : ListPackagesOptions =
    match args.TryGetResult(As_Package_Json) with
    | Some _ -> { format = PackageJson }
    | _ -> { format = HumanReadable }

  interface IArgParserTemplate with
    member this.Usage: string =
      match this with
      | As_Package_Json -> "List packages in npm's package.json format."

type DevServerArgs =
  | [<CliPrefix(CliPrefix.None); AltCommandLine("s")>] Serve of
    ParseResults<ServerArgs>
  | [<CliPrefix(CliPrefix.None); AltCommandLine("b")>] Build of
    ParseResults<BuildArgs>
  | [<CliPrefix(CliPrefix.None)>] Init of ParseResults<InitArgs>
  | [<CliPrefix(CliPrefix.None); AltCommandLine("se")>] Search of
    ParseResults<SearchArgs>
  | [<CliPrefix(CliPrefix.None)>] Show of ParseResults<ShowArgs>
  | [<CliPrefix(CliPrefix.None)>] Add of ParseResults<AddArgs>
  | [<CliPrefix(CliPrefix.None)>] Remove of ParseResults<RemoveArgs>
  | [<CliPrefix(CliPrefix.None)>] List of ParseResults<ListArgs>
  | [<AltCommandLine("-v")>] Version

  interface IArgParserTemplate with
    member this.Usage =
      match this with
      | Serve _ ->
        "Starts a development server for modern Javascript development"
      | Build _ -> "Builds the specified JS and CSS resources for production"
      | Init _ -> "Creates basic files and directories to start using fds."
      | Search _ -> "Searches a package in the skypack API."
      | Show _ -> "Gets the skypack information about a package."
      | Add _ -> "Generates an entry in the import map."
      | Remove _ -> "Removes an entry in the import map."
      | List _ -> "List entries in the import map."
      | Version _ -> "Prints out the cli version to the console."

module Commands =

  let getServerOptions (serverargs: ServerArgs list) =
    let config =
      match Fs.getFdsConfig (Fs.Paths.GetFdsConfigPath()) with
      | Ok config -> config
      | Error err ->
        eprintfn "%s" err.Message
        FdsConfig.DefaultConfig()

    let devServerConfig =
      match config.devServer with
      | Some devServer -> devServer
      | None -> DevServerConfig.DefaultConfig()

    let foldServerOpts (server: DevServerConfig) (next: ServerArgs) =

      match next with
      | Auto_Start (Some value) -> { server with autoStart = Some value }
      | Port (Some value) -> { server with port = Some value }
      | Host (Some value) -> { server with host = Some value }
      | Use_Ssl (Some value) -> { server with useSSL = Some value }
      | _ -> server

    { config with
        devServer =
          serverargs
          |> List.fold foldServerOpts devServerConfig
          |> Some }

  let getBuildOptions (serverargs: BuildArgs list) =
    let config =
      match Fs.getFdsConfig (Fs.Paths.GetFdsConfigPath()) with
      | Ok config -> config
      | Error err ->
        eprintfn "%s" err.Message
        FdsConfig.DefaultConfig()

    let buildConfig =
      match config.build with
      | Some build -> build
      | None -> BuildConfig.DefaultConfig()

    let foldBuildOptions (build: BuildConfig) (next: BuildArgs) =
      match next with
      | Esbuild_Version (Some value) ->
        { build with
            esbuildVersion = Some value }
      | Out_Dir (Some value) -> { build with outDir = Some value }
      | _ -> build

    { config with
        build =
          serverargs
          |> List.fold foldBuildOptions buildConfig
          |> Some }

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

  let runList (options: ListPackagesOptions) =
    let (|ParseRegex|_|) regex str =
      let m =
        Text.RegularExpressions.Regex(regex).Match(str)

      if m.Success then
        Some(List.tail [ for x in m.Groups -> x.Value ])
      else
        None

    taskResult {
      let parseUrl url =
        match url with
        | ParseRegex @"https://cdn.skypack.dev/pin/(@?[^@]+)@v([\d.]+)"
                     [ name; version ]
        | ParseRegex @"https://cdn.jsdelivr.net/npm/(@?[^@]+)@([\d.]+)"
                     [ name; version ]
        | ParseRegex @"https://ga.jspm.io/npm:(@?[^@]+)@([\d.]+)"
                     [ name; version ]
        | ParseRegex @"https://unpkg.com/(@?[^@]+)@([\d.]+)" [ name; version ] ->
          Some(name, version)
        | _ -> None

      let! config = Fs.getFdsConfig (GetFdsConfigPath())
      let installedPackages = config.packages |> Option.defaultValue Map.empty
      match options.format with
      | HumanReadable ->
        printfn "Installed packages (alias: packageName@version)"
        printfn ""
        for importMap in installedPackages do
          match parseUrl importMap.Value with
          | Some (name, version) -> printfn $"{importMap.Key}: {name}@{version}"
          | None -> printfn $"{importMap.Key}: Couldn't parse {importMap.Value}"
      | PackageJson ->
        installedPackages
        |> Map.toList
        |> List.choose (fun (_alias, importMap) -> parseUrl importMap)
        |> Map.ofList
        |> Json.ToPackageJson
        |> printfn "%s"
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

  let private tryExecPerlaCommand (command: string) =
    asyncResult {
      let parser = ArgumentParser.Create<DevServerArgs>()

      let parsed =
        parser.Parse(
          command.Split(' '),
          ignoreMissing = true,
          ignoreUnrecognized = true
        )

      match parsed.TryGetSubCommand() with
      | Some (Build _)
      | Some (Serve _)
      | Some (Init _) ->
        return!
          exn "This command is not supported in interactive mode"
          |> Error
      | Some (Add subcmd) ->
        return!
          subcmd
          |> AddArgs.ToOptions
          |> runAdd
          |> Async.AwaitTask
          |> Async.Ignore
      | Some (Remove subcmd) ->
        return!
          subcmd
          |> RemoveArgs.ToOptions
          |> runRemove
          |> Async.AwaitTask
          |> Async.Ignore
      | Some (Search subcmd) ->
        return!
          subcmd
          |> SearchArgs.ToOptions
          |> runSearch
          |> Async.AwaitTask
          |> Async.Ignore
      | Some (Show subcmd) ->
        return!
          subcmd
          |> ShowArgs.ToOptions
          |> runShow
          |> Async.AwaitTask
          |> Async.Ignore
      | Some (List subcmd) ->
        return!
          subcmd
          |> ListArgs.ToOptions
          |> runList
          |> Async.AwaitTask
          |> Async.Ignore
      | err ->
        parsed.Raise($"Hello Commands Specified {err}", showUsage = true)
        return! CommandNotParsedException $"%A{err}" |> Error
    }

  let startInteractive (configuration: FdsConfig) =
    let onStdinAsync =
      serverActions tryExecPerlaCommand configuration

    let deServer =
      defaultArg configuration.devServer (DevServerConfig.DefaultConfig())

    let fableConfig =
      defaultArg configuration.fable (FableConfig.DefaultConfig())

    let autoStartServer = defaultArg deServer.autoStart true
    let autoStartFable = defaultArg fableConfig.autoStart true

    let esbuildVersion =
      configuration.build
      |> Option.map (fun build -> build.esbuildVersion)
      |> Option.flatten
      |> Option.defaultValue Constants.Esbuild_Version

    Esbuild.setupEsbuild esbuildVersion
    |> Async.AwaitTask
    |> Async.StartImmediate

    Console.CancelKeyPress.Add
      (fun _ ->
        printfn "Got it, see you around!..."
        exit 0)

    asyncSeq {
      if autoStartServer then "start"
      if autoStartFable then "start:fable"

      while true do
        let! value = Console.In.ReadLineAsync() |> Async.AwaitTask
        value
    }
    |> AsyncSeq.iterAsync onStdinAsync
