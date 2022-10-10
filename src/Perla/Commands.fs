namespace Perla

open System
open System.IO
open System.Threading.Tasks
open FSharp.Control
open FsToolkit.ErrorHandling
open FSharp.Control.Reactive

open Spectre.Console

open Perla
open Perla.Types
open Perla.Server
open Perla.Build
open Perla.Logger
open Perla.VirtualFs
open Perla.Scaffolding
open Perla.Plugins.Extensibility

open Argu



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
  | [<AltCommandLine("-k")>] Init_Kind of InitKind option
  | [<AltCommandLine("-y")>] Yes of bool option


  static member ToOptions(args: ParseResults<InitArgs>) : InitOptions =
    { path = args.TryGetResult(Path) |> Option.flatten
      withFable = args.TryGetResult(With_Fable) |> Option.flatten
      initKind = args.TryGetResult(Init_Kind) |> Option.flatten
      yes = args.TryGetResult(Yes) |> Option.flatten }

  interface IArgParserTemplate with
    member this.Usage: string =
      match this with
      | Path _ -> "Where to write the config file"
      | With_Fable _ -> "Includes fable options in the config file"
      | Init_Kind _ ->
        "Sets whether to do a full perla setup or just create a perla.json file."
      | Yes _ -> "Skips the full init prompt"

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
  | [<MainCommand>] Package of string

  static member ToOptions
    (args: ParseResults<RemoveArgs>)
    : RemovePackageOptions =
    { package = args.TryGetResult(Package) }

  interface IArgParserTemplate with
    member this.Usage: string =
      match this with
      | Package _ -> "Package name (or alias) to remove from the import map."

type AddArgs =
  | [<MainCommand>] Package of string
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

[<RequireQualifiedAccess>]
type RestoreArgs =
  | [<AltCommandLine("-s")>] Source of Source

  static member ToOptions(args: ParseResults<RestoreArgs>) : RestoreOptions =
    { source =
        args.TryGetResult(RestoreArgs.Source) |> Option.defaultValue Source.Jspm }

  interface IArgParserTemplate with
    member s.Usage =
      match s with
      | Source _ -> "Where to pull dependencies from. defaults to Jspm"

type RepositoryArgs =
  | [<AltCommandLine("-n")>] Repository_Name of string
  | [<AltCommandLine("-b")>] Branch of string option

  static member ToOptions
    (args: ParseResults<RepositoryArgs>)
    : RepositoryOptions =
    { fullRepositoryName = args.GetResult(Repository_Name)
      branch = args.GetResult(Branch) |> Option.defaultValue "main" }

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
  | [<CliPrefix(CliPrefix.None)>] Restore of ParseResults<RestoreArgs>
  | [<CliPrefix(CliPrefix.None)>] Remove of ParseResults<RemoveArgs>
  | [<CliPrefix(CliPrefix.None)>] List of ParseResults<ListArgs>
  | [<CliPrefix(CliPrefix.None)>] New of ParseResults<NewProjectArgs>
  | [<CliPrefix(CliPrefix.None)>] Add_Template of ParseResults<RepositoryArgs>
  | [<CliPrefix(CliPrefix.None)>] Update_Template of
    ParseResults<RepositoryArgs>
  | [<CliPrefix(CliPrefix.None); AltCommandLine("-lt")>] List_Templates
  | [<CliPrefix(CliPrefix.None); AltCommandLine("-rt")>] Remove_Template of
    string
  | [<AltCommandLine("-v")>] Version

  interface IArgParserTemplate with
    member this.Usage =
      match this with
      | Serve _ ->
        "Starts a development server for modern Javascript development"
      | Build _ -> "Builds the specified JS and CSS resources for production"
      | Init _ -> "Sets perla up to start new projects."
      | Search _ -> "Searches a package in the skypack API."
      | Show _ -> "Gets the skypack information about a package."
      | Add _ -> "Generates an entry in the import map."
      | Restore _ -> "Restores import map"
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
  open Perla.PackageManager.Types

  let private (|ParseRegex|_|) regex str =
    let m = Text.RegularExpressions.Regex(regex).Match(str)

    if m.Success then
      Some(List.tail [ for x in m.Groups -> x.Value ])
    else
      None

  let parseUrl url =
    match url with
    | ParseRegex @"https://cdn.skypack.dev/pin/(@?[^@]+)@v([\d.]+)"
                 [ name; version ] -> Some(Source.Skypack, name, version)
    | ParseRegex @"https://cdn.jsdelivr.net/npm/(@?[^@]+)@([\d.]+)"
                 [ name; version ] -> Some(Source.Jsdelivr, name, version)
    | ParseRegex @"https://ga.jspm.io/npm:(@?[^@]+)@([\d.]+)" [ name; version ] ->
      Some(Source.Jspm, name, version)
    | ParseRegex @"https://unpkg.com/(@?[^@]+)@([\d.]+)" [ name; version ] ->
      Some(Source.Unpkg, name, version)
    | _ -> None

  let getServerOptions (serverargs: ServerArgs list) =
    let config =
      match Fs.getPerlaConfig (Path.GetPerlaConfigPath()) with
      | Ok config -> config
      | Error err ->
        Logger.log ("Failed to get perla config, using defaults", err)
        PerlaConfig.DefaultConfig()

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
          serverargs |> List.fold foldServerOpts devServerConfig |> Some }

  let getBuildOptions (serverargs: BuildArgs list) =
    let config =
      match Fs.getPerlaConfig (Path.GetPerlaConfigPath()) with
      | Ok config -> config
      | Error err ->
        Logger.log ("Failed to get perla config, using defaults", err)
        PerlaConfig.DefaultConfig()

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
        build = serverargs |> List.fold foldBuildOptions buildConfig |> Some }

  let startBuild (configuration: PerlaConfig) = execBuild configuration


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
      fullRepoName.Split("/") |> Array.filter (String.IsNullOrWhiteSpace >> not)
    with
    | [| _; repoName |] -> Ok repoName
    | [| _ |] -> Error MissingRepoName
    | _ -> Error WrongGithubFormat

  let private getTemplateAndChild (templateName: string) =
    match
      templateName.Split("/") |> Array.filter (String.IsNullOrWhiteSpace >> not)
    with
    | [| user; template; child |] -> Some user, template, Some child
    | [| template; child |] -> None, template, Some child
    | [| template |] -> None, template, None
    | _ -> None, templateName, None

  let runListTemplates () =
    let results = Templates.List()

    let table =
      Table()
        .AddColumn("Name")
        .AddColumn("Templates")
        .AddColumn("Template Branch")
        .AddColumn("Last Update")
        .AddColumn("Location")

    for column in table.Columns do
      column.Alignment <- Justify.Center

    for result in results do
      let printedDate =
        result.updatedAt
        |> Option.ofNullable
        |> Option.defaultValue result.createdAt
        |> (fun x -> x.ToShortDateString())

      let children =
        let names =
          PerlaFs.GetTemplateChildren result.path
          |> Seq.map (fun d -> $"[green]{d}[/]")

        String.Join("\n", names) |> Markup

      let path =
        let path = TextPath(result.path)
        path.LeafStyle <- Style(Color.Green)
        path.StemStyle <- Style(Color.Yellow)
        path.SeparatorStyle <- Style(Color.Blue)
        path

      table.AddRow(
        Markup($"[yellow]{result.fullName}[/]"),
        children,
        Markup(result.branch),
        Markup(printedDate),
        path
      )
      |> ignore

    AnsiConsole.Write table

    0

  let private updateTemplate
    (template: PerlaTemplateRepository)
    (branch: string)
    (context: StatusContext)
    =
    context.Status <-
      $"Download and extracting template {template.fullName}/{branch}"

    Templates.Update({ template with branch = branch })

  let private addTemplate
    (simpleName: string)
    (fullName: string)
    (branch: string)
    (path: string)
    (context: StatusContext)
    =
    context.Status <- $"Download and extracting template {fullName}/{branch}"

    Templates.Add(simpleName, fullName, branch, path)

  let runAddTemplate (autoContinue: bool option) (opts: RepositoryOptions) =
    taskResult {
      let autoContinue = defaultArg autoContinue false

      let! simpleName =
        getRepositoryName opts.fullRepositoryName
        |> Result.mapError (fun err ->
          err.AsString |> FailedToParseNameException)

      let template =
        opts.fullRepositoryName |> NameKind.FullName |> Templates.FindOne

      match template with
      | Some template ->
        if autoContinue then
          match!
            Logger.spinner (
              "Updating Templates",
              updateTemplate template opts.branch
            )
          with
          | true ->
            Logger.log
              $"{opts.fullRepositoryName} - {opts.branch} updated correctly"

            return 0
          | false ->
            Logger.log
              $"{opts.fullRepositoryName} - {opts.branch} was not updated"

            return 1
        else
          let prompt = SelectionPrompt<string>().AddChoices([ "Yes"; "No" ])

          prompt.Title <-
            $"\"{opts.fullRepositoryName}\" already exists, Do you want to update it?"

          match AnsiConsole.Prompt(prompt) with
          | "Yes" ->
            match!
              Logger.spinner (
                "Updating Templates",
                updateTemplate template opts.branch
              )
            with
            | true ->
              Logger.log
                $"{opts.fullRepositoryName} - {opts.branch} updated correctly"

              return 0
            | false ->
              Logger.log
                $"{opts.fullRepositoryName} - {opts.branch} was not updated"

              return 1
          | _ ->
            Logger.log $"Template {template.fullName} was not updated"
            return 0
      | None ->
        let path =
          Path.Combine(PerlaFs.Templates, opts.fullRepositoryName, opts.branch)

        let! tplId =
          Logger.spinner (
            "Adding new templates",
            addTemplate simpleName opts.fullRepositoryName opts.branch path
          )

        match Templates.FindOne(tplId) with
        | Some template ->
          Logger.log $"Succesfully added {template.fullName} at {template.path}"
          return 0
        | None ->
          Logger.log
            $"Template may have been downloaded but for some reason we can't find it, this is likely a bug"

          return 1

    }


  let runInit (options: InitOptions) =
    taskResult {
      let path = Path.GetPerlaConfigPath(?directoryPath = options.path)

      let initKind = defaultArg options.initKind InitKind.Full

      match initKind with
      | InitKind.Full ->
        Logger.log "Perla will set up the following resources:"
        Logger.log "- Esbuild"
        Logger.log "- Default Templates"

        Logger.log
          "After that you should be able to run 'perla build' or 'perla new'"

        let canContinue =
          match options.yes with
          | Some true -> true
          | _ ->
            let prompt = SelectionPrompt<string>().AddChoices([ "Yes"; "No" ])

            prompt.Title <- $"Can we Start?"

            match AnsiConsole.Prompt(prompt) with
            | "Yes" -> true
            | _ -> false

        if not <| canContinue then
          Logger.log "Nothing to do, finishing here"
          return 0
        else
          do! Esbuild.setupEsbuild Constants.Esbuild_Version

          let! res =
            runAddTemplate
              (Some true)
              { branch = Constants.Default_Templates_Repository_Branch
                fullRepositoryName = Constants.Default_Templates_Repository }

          if res <> 0 then
            return res
          else
            Logger.log (
              "[bold green]esbuild[/] and [bold yellow]templates[/] have been setup!",
              escape = false
            )

            runListTemplates () |> ignore
            Logger.log "Feel free to create a new perla project"

            Logger.log (
              "[bold yellow]perla[/] [bold blue]new -t[/] [bold green]perla-templates/<TEMPLATE_NAME>[/] [bold blue]-n <PROJECT_NAME>[/]",
              escape = false
            )

            return 0
      | InitKind.Simple ->
        let config =
          PerlaConfig.DefaultConfig(defaultArg options.withFable false)

        let fable =
          config.fable
          |> Option.map (fun fable -> { fable with autoStart = Some true })

        let config =
          {| ``$schema`` = config.``$schema``
             index = config.index
             fable = fable |}

        do! Fs.createPerlaConfig path config

        return 0
      | _ ->
        return!
          (ArgumentException "The provided kind is not supported" :> exn)
          |> Error
    }



  let runSearch (options: SearchOptions) =
    taskResult {
      let! package =
        match options.package with
        | Some package -> Ok package
        | None -> Error PackageNotFoundException

      do! PackageSearch.searchPackage (package, defaultArg options.page 1)
      return 0
    }



  let runShow (options: ShowPackageOptions) =
    taskResult {
      let! package =
        match options.package with
        | Some package -> Ok package
        | None -> Error PackageNotFoundException

      do! PackageSearch.showPackage (package)
      return 0
    }

  let runList (options: ListPackagesOptions) =
    taskResult {
      let! config = Fs.getPerlaConfig (Path.GetPerlaConfigPath())

      let installedPackages = config.packages |> Option.defaultValue Map.empty

      match options.format with
      | HumanReadable ->
        Logger.log (
          "[bold green]Installed packages[/] [yellow](alias: packageName@version)[/]\n",
          escape = false
        )

        for importMap in installedPackages do
          match parseUrl importMap.Value with
          | Some (_, name, version) ->
            Logger.log (
              $"[bold yellow]{importMap.Key}[/]: [green]{name}@{version}[/]",
              escape = false
            )
          | None ->
            Logger.log (
              $"[bold red]{importMap.Key}[/]: [yellow]Couldn't parse {importMap.Value}[/]",
              escape = false
            )
      | PackageJson ->
        installedPackages
        |> Map.toList
        |> List.choose (fun (_alias, importMap) ->
          parseUrl importMap
          |> Option.map (fun (_, name, version) -> (name, version)))
        |> Map.ofList
        |> Json.ToPackageJson
        |> printfn "%s"

      return 0
    }

  let runRemove (options: RemovePackageOptions) =
    taskResult {
      let name = defaultArg options.package ""
      Logger.log ($"Removing: [red]{name}[/]", escape = false)

      if name = "" then
        return! PackageNotFoundException |> Error

      let! fdsConfig = Fs.getPerlaConfig (Path.GetPerlaConfigPath())
      let! lockFile = Fs.getOrCreateLockFile (Path.GetPerlaConfigPath())

      let deps =
        fdsConfig.packages |> Option.map (fun map -> map |> Map.remove name)

      let opts = { fdsConfig with packages = deps }

      let imports = lockFile.imports |> Map.remove name

      let scopes =
        defaultArg lockFile.scopes Map.empty
        |> Map.map (fun _ value -> value |> Map.remove name)

      Logger.log ("Updating importmap...")
      Logger.log ($"Writing scopes: %A{scopes}")
      Logger.log ($"Writing imports: %A{imports}")

      do!
        Fs.writeLockFile
          (Path.GetPerlaConfigPath())
          { lockFile with
              scopes = Some scopes
              imports = imports }

      do! Fs.createPerlaConfig (Path.GetPerlaConfigPath()) opts

      return 0
    }

  let runNew (opts: ProjectOptions) =
    Logger.log ("Creating new project...")
    let (user, template, child) = getTemplateAndChild opts.templateName

    result {
      let repository =
        match user, child with
        | Some user, Some _ ->
          Templates.FindOne(NameKind.FullName $"{user}/{template}")
        | Some _, None -> Templates.FindOne(NameKind.FullName opts.templateName)
        | None, _ -> Templates.FindOne(NameKind.Name template)

      match repository with
      | Some clamRepo ->
        Logger.log (
          $"Using [bold yellow]{clamRepo.name}:{clamRepo.branch}[/]",
          escape = false
        )

        let templatePath =
          PerlaFs.GetPathForTemplate(
            clamRepo.name,
            clamRepo.branch,
            ?child = child
          )

        let targetPath = $"./{opts.projectName}"

        let content =
          PerlaFs.GetTemplateScriptContent(templatePath, clamRepo.path)
          |> Option.map (Scaffolding.getConfigurationFromScript)
          |> Option.flatten

        Logger.log ($"Creating structure...")

        PerlaFs.WriteTemplateToDisk(
          templatePath,
          targetPath,
          ?payload = content
        )

        return 0
      | None ->
        Logger.log $"Template [{opts.templateName}] was not found"
        return 1

    }

  let runRemoveTemplate (name: string) =
    let deleteOperation =
      option {
        let! repo = Templates.FindOne(NameKind.FullName name)
        PerlaFs.RemoveTemplateFromDisk repo.path
        return! Templates.Delete repo.fullName
      }

    match deleteOperation with
    | Some true ->
      Logger.log (
        $"[bold yellow]{name}[/] deleted from repositories.",
        escape = false
      )

      Ok 0
    | Some false ->
      Logger.log (
        $"[bold red]{name}[/] could not be deleted from repositories.",
        escape = false
      )

      Ok 1
    | None ->
      Logger.log (
        $"[bold red]{name}[/] was not found in the repository list.",
        escape = false
      )

      Ok 1

  let runUpdateTemplate (opts: RepositoryOptions) =
    taskResult {

      match Templates.FindOne(NameKind.FullName opts.fullRepositoryName) with
      | Some template ->
        match!
          Logger.spinner (
            "Updating Template",
            updateTemplate template opts.branch
          )
        with
        | true ->
          Logger.log (
            $"[bold green]{opts.fullRepositoryName}[/] - [yellow]{opts.branch}[/] updated correctly",
            escape = false
          )

          return 0
        | _ ->
          Logger.log (
            $"[bold red]{opts.fullRepositoryName}[/] - [yellow]{opts.branch}[/] failed to update",
            escape = false
          )

          return 1
      | None ->
        Logger.log (
          $"[bold red]{opts.fullRepositoryName}[/] - [yellow]{opts.branch}[/] failed to update",
          escape = false
        )

        return 1

    }

  let runAdd (options: AddPackageOptions) =
    taskResult {
      let! package, version =
        match options.package with
        | Some package -> parsePackageName package |> Ok
        | None -> Error(exn "Missing package name")

      match options.alias with
      | Some _ ->
        Logger.log (
          $"[bold yellow]Aliases management will change in the future, they will be ignored if this warning appears[/]",
          escape = false
        )
      | None -> ()

      let provider =
        defaultArg options.source Source.Jspm
        |> function
          | Source.Skypack -> Provider.Skypack
          | Source.Unpkg -> Provider.Unpkg
          | Source.Jsdelivr -> Provider.Jsdelivr
          | Source.JspmSystem -> Provider.JspmSystem
          | Source.Jspm
          | _ -> Provider.Jspm

      let version =
        match version with
        | Some version -> $"@{version}"
        | None -> ""

      let importMap = PerlaFs.ImportMap()
      Logger.log "Updating importmap..."

      let! map =
        Logger.spinner (
          $"Adding: [bold yellow]{package}{version}[/]",
          Dependencies.Add($"{package}{version}", importMap, provider)
        )
        |> TaskResult.mapError (fun err -> exn err)

      PerlaFs.WriteMap(map)
      return 0
    }

  let runRestore (options: RestoreOptions) =
    taskResult {
      let importMap = PerlaFs.ImportMap()

      Logger.log "Regenerating import map..."

      let packages = importMap.imports |> Map.keys

      let provider =
        options.source
        |> function
          | Source.Skypack -> Provider.Skypack
          | Source.Unpkg -> Provider.Unpkg
          | Source.Jsdelivr -> Provider.Jsdelivr
          | Source.JspmSystem -> Provider.JspmSystem
          | Source.Jspm
          | _ -> Provider.Jspm

      let! newMap =
        Dependencies.Restore(packages, provider = provider)
        |> TaskResult.mapError (fun err -> exn err)

      PerlaFs.WriteMap(newMap)
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
        return! exn "This command is not supported in interactive mode" |> Error
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

  let startInteractive (configuration: unit -> PerlaConfig) =
    let onStdinAsync = serverActions tryExecPerlaCommand configuration
    let perlaWatcher = Fs.getPerlaConfigWatcher ()
    let configuration = configuration ()

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

    Console.CancelKeyPress.Add(fun _ ->
      Logger.log "Got it, see you around!..."
      onStdinAsync "exit" |> Async.RunSynchronously
      exit 0)

    [ perlaWatcher.Changed
      |> Observable.throttle (TimeSpan.FromMilliseconds(400.))
      perlaWatcher.Created
      |> Observable.throttle (TimeSpan.FromMilliseconds(400.)) ]
    |> Observable.mergeSeq
    |> Observable.map (fun _ -> onStdinAsync "restart")
    |> Observable.switchAsync
    |> Observable.add (fun _ -> Logger.log "perla.jsonc Changed, Restarting")

    asyncSeq {
      if autoStartServer then
        "start"

      if autoStartFable then
        "start:fable"

      while true do
        let! value = Console.In.ReadLineAsync() |> Async.AwaitTask
        value
    }
    |> AsyncSeq.iterAsync onStdinAsync
