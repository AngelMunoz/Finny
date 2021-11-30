namespace Perla

open System
open FSharp.Control
open FsToolkit.ErrorHandling
open Clam
open Clam.Types
open Perla
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
      | Port _ -> "Selects the server port, defaults to 7331"
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
        "Uses a specific esbuild version. defaults to \"0.12.9\""
      | Out_Dir _ -> "Where to output the files. Defaults to \"./dist\""

type InitArgs =
  | [<AltCommandLine("-p")>] Path of string option
  | [<AltCommandLine("-wf")>] With_Fable of bool option

  static member ToOptions(args: ParseResults<InitArgs>) : InitOptions =
    { path = args.TryGetResult(Path) |> Option.flatten
      withFable = args.TryGetResult(With_Fable) |> Option.flatten }

  interface IArgParserTemplate with
    member this.Usage: string =
      match this with
      | Path _ -> "Where to write the config file"
      | With_Fable _ -> "Includes fable options in the config file"

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
      | Package _ -> "Package name (or alias) to remove from the import map."

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
      | As_Package_Json -> "Lists packages in npm's package.json format."

type RepositoryArgs =
  | [<AltCommandLine("-n")>] Repository_Name of string
  | [<AltCommandLine("-b")>] Branch of string option

  static member ToOptions
    (args: ParseResults<RepositoryArgs>)
    : RepositoryOptions =
    { fullRepositoryName = args.GetResult(Repository_Name)
      branch =
        args.GetResult(Branch)
        |> Option.defaultValue "main" }

  interface IArgParserTemplate with
    member s.Usage =
      match s with
      | Repository_Name _ -> "Name of the repository where the template lives"
      | Branch _ -> "Branch to pick the repository from, defaults to \"main\""

type NewProjectArgs =
  | [<AltCommandLine("-t")>] Template of string
  | [<AltCommandLine("-n")>] ProjectName of string

  static member ToOptions(args: ParseResults<NewProjectArgs>) : ProjectOptions =
    { projectName = args.GetResult(ProjectName)
      templateName = args.GetResult(Template) }

  interface IArgParserTemplate with
    member s.Usage =
      match s with
      | ProjectName _ -> "Name of the project to create."
      | Template _ -> "Template to use for this project."

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
  | [<CliPrefix(CliPrefix.None)>] New of ParseResults<NewProjectArgs>
  | [<CliPrefix(CliPrefix.None)>] Add_Template of ParseResults<RepositoryArgs>
  | [<CliPrefix(CliPrefix.None)>] Update_Template of
    ParseResults<RepositoryArgs>
  | [<AltCommandLine("-lt")>] List_Templates
  | [<AltCommandLine("-rt")>] Remove_Template of string
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
      | List _ -> "Lists entries in the import map."
      | New _ -> "Creates a new Perla based project."
      | List_Templates -> "Shows existing templates available to scaffold."
      | Add_Template _ ->
        "Downloads a GitHub repository to the templates directory."
      | Update_Template _ ->
        "Downloads a new version of the specified template."
      | Remove_Template _ -> "Removes an existing templating repository."
      | Version _ -> "Prints out the cli version to the console."

module Commands =

  let getServerOptions (serverargs: ServerArgs list) =
    let config =
      match Fs.getPerlaConfig (Fs.Paths.GetPerlaConfigPath()) with
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
      match Fs.getPerlaConfig (Fs.Paths.GetPerlaConfigPath()) with
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
        { build with esbuildVersion = Some value }
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
        let version = parts |> Seq.tryLast |> Option.defaultValue ""

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

  let private getRepositoryName (fullRepoName: string) =
    match
      fullRepoName.Split("/")
      |> Array.filter (String.IsNullOrWhiteSpace >> not)
      with
    | [| _; repoName |] -> Ok repoName
    | [| _ |] -> Error MissingRepoName
    | _ -> Error WrongGithubFormat

  let private getTemplateAndChild (templateName: string) =
    match
      templateName.Split("/")
      |> Array.filter (String.IsNullOrWhiteSpace >> not)
      with
    | [| user; template; child |] -> Some user, template, Some child
    | [| template; child |] -> None, template, Some child
    | [| template |] -> None, template, None
    | _ -> None, templateName, None


  let runInit (options: InitOptions) =
    result {
      let path =
        match options.path with
        | Some path -> GetPerlaConfigPath(path)
        | None -> GetPerlaConfigPath()

      let config = FdsConfig.DefaultConfig(defaultArg options.withFable false)

      let fable =
        config.fable
        |> Option.map (fun fable -> { fable with autoStart = Some true })

      let config =
        {| ``$schema`` = config.``$schema``
           index = config.index
           fable = fable |}

      do! Fs.createPerlaConfig path config

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
      |> Seq.iter (fun package ->
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
      let m = Text.RegularExpressions.Regex(regex).Match(str)

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

      let! config = Fs.getPerlaConfig (GetPerlaConfigPath())

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

      let! fdsConfig = Fs.getPerlaConfig (GetPerlaConfigPath())
      let! lockFile = Fs.getOrCreateLockFile (GetPerlaConfigPath())

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
          (GetPerlaConfigPath())
          { lockFile with
              scopes = scopes
              imports = imports }

      do! Fs.createPerlaConfig (GetPerlaConfigPath()) opts

      return 0
    }

  let runNew (opts: ProjectOptions) =
    let (user, template, child) = getTemplateAndChild opts.templateName

    result {
      let repository =
        match user, child with
        | Some user, Some _ -> Database.findByFullName $"{user}/{template}"
        | Some _, None -> Database.findByFullName opts.templateName
        | None, _ -> Database.findByName template

      match repository with
      | Some clamRepo ->
        let templatePath = Fs.getClamTplPath clamRepo child
        let targetPath = Fs.getClamTplTarget opts.projectName

        let content = Fs.getClamTplScriptContent templatePath clamRepo.path

        match content with
        | Some content ->
          Extensibility.getConfigurationFromScript content
          |> Scaffolding.compileAndCopy templatePath targetPath
        | None -> Scaffolding.compileAndCopy templatePath targetPath None

        return 0
      | None ->
        return!
          TemplateNotFoundException
            $"Template [{opts.templateName}] was not found"
          |> Error
    }

  let runListTemplates () =
    let results = Database.listEntries ()

    for result in results do
      let printedDate =
        result.updatedAt
        |> Option.ofNullable
        |> Option.defaultValue result.createdAt
        |> (fun x -> x.ToShortDateString())

      printfn $"[{printedDate}] - [{result.fullName}] - {result.path}"

    0

  let runAddTemplate (opts: RepositoryOptions) =
    result {
      match getRepositoryName opts.fullRepositoryName with
      | Error err ->
        return!
          err.AsString
          |> FailedToParseNameException
          |> Error
      | Ok simpleName ->
        if Database.existsByFullName opts.fullRepositoryName then
          printfn
            "\"{opts.repositoryName}\" already exists, Do you want to update it? [y/N]"

          match Console.ReadKey().Key with
          | ConsoleKey.Y ->
            let updateOperation =
              option {
                let! repo = Database.findByFullName opts.fullRepositoryName
                let repo = { repo with branch = opts.branch }

                return!
                  repo
                  |> Scaffolding.downloadRepo
                  |> Scaffolding.unzipAndClean
                  |> Database.updateEntry
              }

            match updateOperation with
            | Some true ->
              printfn
                $"{opts.fullRepositoryName} - {opts.branch} updated correctly"

              return 0
            | _ -> return! UpdateTemplateFailedException |> Error
          | _ -> return 0
        else
          let path = Fs.getClamRepoPath opts.fullRepositoryName opts.branch

          let addedRepository =
            (simpleName, opts.fullRepositoryName, opts.branch)
            |> (ClamRepo.NewClamRepo path)
            |> Scaffolding.downloadRepo
            |> Scaffolding.unzipAndClean
            |> Database.createEntry

          match addedRepository with
          | Some repository ->
            printfn
              $"Succesfully added {repository.fullName} at {repository.path}"

            return 0
          | None -> return! AddTemplateFailedException |> Error
    }

  let deleteTemplate (name: string) =
    let deleteOperation =
      option {
        let! repo = Database.findByFullName name
        Fs.removeClamRepo repo
        return! Database.deleteByFullName repo.fullName
      }

    match deleteOperation with
    | Some true ->
      printfn $"{name} deleted from repositories."
      Ok 0
    | Some false ->
      printfn $"{name} could not be deleted from repositories."
      DeleteTemplateFailedException |> Error
    | None -> name |> TemplateNotFoundException |> Error

  let runUpdateTemplate (opts: RepositoryOptions) =
    let updateOperation =
      option {
        let! repo = Database.findByFullName opts.fullRepositoryName
        let repo = { repo with branch = opts.branch }

        return!
          repo
          |> Scaffolding.downloadRepo
          |> Scaffolding.unzipAndClean
          |> Database.updateEntry
      }

    match updateOperation with
    | Some true ->
      printfn $"{opts.fullRepositoryName} - {opts.branch} updated correctly"

      Ok 0
    | _ -> UpdateTemplateFailedException |> Error

  let runAdd (options: AddPackageOptions) =
    taskResult {
      let! package, version =
        match options.package with
        | Some package -> parsePackageName package |> Ok
        | None -> MissingPackageNameException |> Error

      let alias = options.alias |> Option.defaultValue package

      let source = defaultArg options.source Source.Skypack

      let version =
        match version with
        | Some version -> $"@{version}"
        | None -> ""

      let! (deps, scopes) =
        Http.getPackageUrlInfo $"{package}{version}" alias source

      let! fdsConfig = Fs.getPerlaConfig (GetPerlaConfigPath())
      let! lockFile = Fs.getOrCreateLockFile (GetPerlaConfigPath())

      let packages =
        fdsConfig.packages
        |> Option.defaultValue Map.empty
        |> Map.toList
        |> fun existing -> existing @ deps
        |> Map.ofList

      let fdsConfig = { fdsConfig with packages = packages |> Some }

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

      do! Fs.createPerlaConfig (GetPerlaConfigPath()) fdsConfig
      do! Fs.writeLockFile (GetPerlaConfigPath()) lockFile

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
        parser.PrintUsage("No command specified", hideSyntax = true)
        |> printfn "%s"

        return! async { return () }
    }

  let startInteractive (configuration: FdsConfig) =
    let onStdinAsync = serverActions tryExecPerlaCommand configuration

    let devServer =
      defaultArg configuration.devServer (DevServerConfig.DefaultConfig())

    let fableConfig =
      defaultArg configuration.fable (FableConfig.DefaultConfig())

    let autoStartServer = defaultArg devServer.autoStart true
    let autoStartFable = defaultArg fableConfig.autoStart true

    let esbuildVersion =
      configuration.build
      |> Option.map (fun build -> build.esbuildVersion)
      |> Option.flatten
      |> Option.defaultValue Constants.Esbuild_Version

    Esbuild.setupEsbuild esbuildVersion
    |> Async.AwaitTask
    |> Async.StartImmediate

    Console.CancelKeyPress.Add (fun _ ->
      printfn "Got it, see you around!..."
      onStdinAsync "exit" |> Async.RunSynchronously
      exit 0)

    asyncSeq {
      if autoStartServer then "start"
      if autoStartFable then "start:fable"

      while true do
        let! value = Console.In.ReadLineAsync() |> Async.AwaitTask
        value
    }
    |> AsyncSeq.iterAsync onStdinAsync
