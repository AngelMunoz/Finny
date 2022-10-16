namespace Perla

open System
open System.IO

open System.Threading.Tasks
open Spectre.Console

open FSharp.Control
open FSharp.Control.Reactive

open FsToolkit.ErrorHandling

open Perla
open Perla.Types
open Perla.Server
open Perla.Build
open Perla.Logger
open Perla.FileSystem
open Perla.Build
open Perla.Json
open Perla.Scaffolding
open Perla.Plugins.Extensibility
open Perla.PackageManager
open Perla.PackageManager.Types
open Perla.Configuration.Types
open Perla.Configuration
open FSharp.UMX

module CliOptions =

  [<Struct; RequireQualifiedAccess>]
  type Init =
    | Full
    | Simple

  [<Struct; RequireQualifiedAccess>]
  type ListFormat =
    | HumanReadable
    | TextOnly

  type ServeOptions =
    { port: int option
      host: string option
      mode: RunConfiguration option
      ssl: bool option }

  type BuildOptions = { mode: RunConfiguration option }

  type InitOptions =
    { path: DirectoryInfo
      useFable: bool
      mode: Init
      yes: bool }

  type SearchOptions = { package: string; page: int }

  type ShowPackageOptions = { package: string }

  type ListTemplatesOptions = { format: ListFormat }

  type AddPackageOptions =
    { package: string
      version: string
      source: Provider
      mode: RunConfiguration
      alias: string option }

  type RemovePackageOptions =
    { package: string
      alias: string option }

  type ListPackagesOptions = { format: ListFormat }

  type TemplateRepositoryOptions =
    { fullRepositoryName: string
      yes: bool
      branch: string }

  type ProjectOptions =
    { projectName: string
      templateName: string }

  type RestoreOptions =
    { source: Provider
      mode: RunConfiguration }

  type Init with

    static member FromString(value: string) =
      match value.ToLowerInvariant() with
      | "full" -> Init.Full
      | "simple"
      | _ -> Init.Simple

open CliOptions

module Handlers =
  open Units

  let runBuild (args: BuildOptions) =

    Configuration.UpdateFromCliArgs(?runConfig = args.mode)
    let config = Configuration.CurrentConfig
    task {
      match config.fable with
        | Some fable ->
          let cmdResult = (Fable.fableCmd false fable).ExecuteAsync()

          Logger.log($"Starting Fable with pid: [{cmdResult.ProcessId}]", target=PrefixKind.Build)
          do! cmdResult.Task :> Task
        | None -> Logger.log("No Fable configuration provided, skipping fable", target=PrefixKind.Build)

      if not <| File.Exists($"{FileSystem.EsbuildBinaryPath}") then
        do! FileSystem.SetupEsbuild(config.esbuild.version)

      let outDir = UMX.untag config.build.outDir
      try
        Directory.Delete(outDir, true)
        Directory.CreateDirectory(outDir) |> ignore
      with ex ->
        ()
      do! Build.Execute config
      return 0
    }

  let runListTemplates (options: ListTemplatesOptions) =
    let results = Templates.List()

    match options.format with
    | ListFormat.HumanReadable ->
      let table =
        Table()
          .AddColumns(
            [| "Name"
               "Templates"
               "Template Branch"
               "Last Update"
               "Location" |]
          )

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
            FileSystem.PathForTemplate(
              result.fullName,
              result.branch,
              result.name
            )
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
    | ListFormat.TextOnly ->
      for result in results do
        Logger.log $"[bold green]{result.fullName}[/] - {result.branch}"

        let path = TextPath(result.path).StemColor(Color.Blue)
        AnsiConsole.Write path

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

  let runAddTemplate (opts: TemplateRepositoryOptions) =
    taskResult {
      let autoContinue = opts.yes

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
          Path.Combine(
            UMX.untag FileSystem.Templates,
            opts.fullRepositoryName,
            opts.branch
          )

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
      let initKind = options.mode

      match initKind with
      | Init.Full ->
        Logger.log "Perla will set up the following resources:"
        Logger.log "- Esbuild"
        Logger.log "- Default Templates"

        Logger.log
          "After that you should be able to run 'perla build' or 'perla new'"

        let canContinue =
          match options.yes with
          | true -> true
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
          do!
            FileSystem.SetupEsbuild(
              FSharp.UMX.UMX.tag Constants.Esbuild_Version
            )

          let! res =
            runAddTemplate
              { branch = Constants.Default_Templates_Repository_Branch
                yes = true
                fullRepositoryName = Constants.Default_Templates_Repository }

          if res <> 0 then
            return res
          else
            Logger.log (
              "[bold green]esbuild[/] and [bold yellow]templates[/] have been setup!",
              escape = false
            )

            runListTemplates { format = ListFormat.HumanReadable } |> ignore
            Logger.log "Feel free to create a new perla project"

            Logger.log (
              "[bold yellow]perla[/] [bold blue]new -t[/] [bold green]perla-templates/<TEMPLATE_NAME>[/] [bold blue]-n <PROJECT_NAME>[/]",
              escape = false
            )

            return 0
      | Init.Simple ->
        match options.useFable with
        | true ->
          Configuration.WriteFieldsToFile(
            [ PerlaWritableField.Fable [ Project Constants.FableProject ] ]
          )

          do!
            FileSystem.GenerateSimpleFable(
              UMX.tag<SystemPath> options.path.FullName
            )
        | false -> ()

        return 0
    }



  let runSearch (options: SearchOptions) =
    task {
      do! PackageSearch.searchPackage (options.package, options.page)
      return 0
    }



  let runShow (options: ShowPackageOptions) =
    task {
      do! PackageSearch.showPackage (options.package)
      return 0
    }

  let runList (options: ListPackagesOptions) =
    task {
      let config = Configuration.CurrentConfig

      match options.format with
      | ListFormat.HumanReadable ->
        Logger.log (
          "[bold green]Installed packages[/] [yellow](alias: packageName@version)[/]\n",
          escape = false
        )

        let prodtable =
          dependencyTable (config.dependencies, "Production Dependencies")

        let devTable =
          dependencyTable (config.devDependencies, "Development Dependencies")

        AnsiConsole.Write(prodtable)
        AnsiConsole.WriteLine()
        AnsiConsole.Write(devTable)

      | ListFormat.TextOnly ->
        let inline aliasDependency (dep: Dependency) =
          let name =
            match dep.alias with
            | Some alias -> $"{alias}:{dep.name}"
            | None -> dep.name

          name, dep.version

        let depsMap =
          config.dependencies |> Seq.map aliasDependency |> Map.ofSeq

        let devDepsMap =
          config.devDependencies |> Seq.map aliasDependency |> Map.ofSeq

        {| dependencies = depsMap
           devDependencies = devDepsMap |}
        |> Json.ToText
        |> AnsiConsole.Write

      return 0
    }

  let runRemove (options: RemovePackageOptions) =
    task {
      let name = options.package
      Logger.log ($"Removing: [red]{name}[/]", escape = false)
      let config = Configuration.CurrentConfig

      let inline filterNameOrAlias (dep: Dependency) =
        match dep.alias with
        | Some alias -> not (dep.name = name || alias = name)
        | None -> dep.name <> name

      let deps =
        option {
          let! dependencies = config.dependencies

          return dependencies |> Seq.filter filterNameOrAlias
        }

      let depDeps =
        option {
          let! devDependencies = config.devDependencies

          return devDependencies |> Seq.filter filterNameOrAlias
        }

      match deps, depDeps with
      | Some deps, Some devDeps ->
        Configuration.WriteFieldsToFile(
          [ PerlaWritableField.Dependencies deps
            PerlaWritableField.DevDependencies devDeps ]
        )
      | Some deps, None ->
        Configuration.WriteFieldsToFile(
          [ PerlaWritableField.Dependencies deps ]
        )
      | None, Some devDeps ->
        Configuration.WriteFieldsToFile(
          [ PerlaWritableField.DevDependencies devDeps ]
        )
      | None, None -> ()


      let deps =
        deps
        |> Option.defaultValue Seq.empty
        |> Seq.map (fun p -> $"{p.name}@{p.version}")

      match! Dependencies.Restore(deps, Provider.Jspm) with
      | Ok map ->
        FileSystem.WriteImportMap(map) |> ignore
        return 0
      | Error err ->
        Logger.log ($"[bold red]{err}[/]", escape = false)
        return 1
    }

  let runNew (opts: ProjectOptions) =
    Logger.log ("Creating new project...")
    let (user, template, child) = getTemplateAndChild opts.templateName

    option {
      let! repository =
        match user, child with
        | Some user, Some _ ->
          Templates.FindOne(NameKind.FullName $"{user}/{template}")
        | Some _, None -> Templates.FindOne(NameKind.FullName opts.templateName)
        | None, _ -> Templates.FindOne(NameKind.Name template)

      Logger.log (
        $"Using [bold yellow]{repository.name}:{repository.branch}[/]",
        escape = false
      )

      let templatePath =
        FileSystem.PathForTemplate(
          repository.fullName,
          repository.branch,
          tplName = repository.name
        )

      let targetPath = $"./{templatePath}"

      let content =
        FileSystem.GetTemplateScriptContent(
          repository.fullName,
          repository.branch,
          repository.name
        )
        |> Option.map (Scaffolding.getConfigurationFromScript)
        |> Option.flatten

      Logger.log ($"Creating structure...")

      FileSystem.WriteTplRepositoryToDisk(
        UMX.tag<SystemPath> templatePath,
        UMX.tag<UserPath> targetPath,
        ?payload = content
      )

      return 0
    }
    |> function
      | None ->
        Logger.log $"Template [{opts.templateName}] was not found"
        1
      | Some n -> n


  let runRemoveTemplate (name: string) =
    let deleteOperation =
      option {
        let! repo = Templates.FindOne(NameKind.FullName name)
        UMX.tag<SystemPath> repo.fullName |> FileSystem.RemoveTemplateDirectory
        return! Templates.Delete repo.fullName
      }

    match deleteOperation with
    | Some true ->
      Logger.log (
        $"[bold yellow]{name}[/] deleted from repositories.",
        escape = false
      )

      0
    | Some false ->
      Logger.log (
        $"[bold red]{name}[/] could not be deleted from repositories.",
        escape = false
      )

      0
    | None ->
      Logger.log (
        $"[bold red]{name}[/] was not found in the repository list.",
        escape = false
      )

      1

  let runUpdateTemplate (opts: TemplateRepositoryOptions) =
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
    task {
      let package, version = parsePackageName options.package

      match options.alias with
      | Some _ ->
        Logger.log (
          $"[bold yellow]Aliases management will change in the future, they will be ignored if this warning appears[/]",
          escape = false
        )
      | None -> ()

      let provider = options.source

      let version =
        match version with
        | Some version -> $"@{version}"
        | None -> ""

      let importMap = FileSystem.ImportMap()
      Logger.log "Updating importmap..."

      let! map =
        Logger.spinner (
          $"Adding: [bold yellow]{package}{version}[/]",
          Dependencies.Add($"{package}{version}", importMap, provider)
        )

      match map with
      | Ok map ->
        FileSystem.WriteImportMap(map) |> ignore
        return 0
      | Error err ->
        Logger.log ($"[bold red]{err}[/]", escape = false)
        return 1
    }

  let runRestore (options: RestoreOptions) =
    taskResult {
      let importMap = FileSystem.ImportMap()

      Logger.log "Regenerating import map..."

      let packages = importMap.imports |> Map.keys

      let provider = options.source

      let! newMap =
        Dependencies.Restore(packages, provider = provider)
        |> TaskResult.mapError (fun err -> exn err)

      FileSystem.WriteImportMap(newMap) |> ignore
      return 0
    }


  let runVersion (isSemver: bool) : int =
    let version =
      System.Reflection.Assembly.GetEntryAssembly().GetName().Version

    if isSemver then
      Logger.log $"{version.Major}.{version.Minor}.{version.Revision}"
    else
      Logger.log $"{version}"

    0

  let runServe (configuration: ServeOptions) =
    let configuration = Configuration.CurrentConfig
    let withFable = configuration.fable.IsSome
    // let perlaWatcher = Fs.getPerlaConfigWatcher ()


    FileSystem.SetupEsbuild configuration.esbuild.version
    |> Async.AwaitTask
    |> Async.StartImmediate

    let startServer () = failwith ""
    let startFable () = failwith ""

    let watchForChanges () = failwith ""

    Console.CancelKeyPress.Add(fun _ ->
      Logger.log "Got it, see you around!..."

      exit 0)


module Commands =

  open FSharp.SystemCommandLine
  open FSharp.SystemCommandLine.Aliases

  [<AutoOpen>]
  module Inputs =
    open System.CommandLine

    type Input with

      static member OptionWithStrings
        (
          aliases: string seq,
          values: string seq,
          defaultValue: string,
          ?description
        ) =
        Opt<string>(
          aliases |> Array.ofSeq,
          getDefaultValue = (fun _ -> defaultValue),
          ?description = description
        )
          .FromAmong(values |> Array.ofSeq)
        |> HandlerInput.OfOption

      static member ArgumentWithStrings
        (
          name: string,
          defaultValue: string,
          values: string seq,
          ?description
        ) =
        Arg<string>(
          name,
          getDefaultValue = (fun _ -> defaultValue),
          ?description = description
        )
          .FromAmong(values |> Array.ofSeq)
        |> HandlerInput.OfArgument

      static member ArgumentMaybe
        (
          name: string,
          values: string seq,
          ?description
        ) =
        Arg<string option>(
          name,
          parse =
            (fun argResult ->
              match argResult.Tokens |> Seq.toList with
              | [] -> None
              | [ token ] -> Some token.Value
              | _ :: _ ->
                failwith "F# Option can only be used with a single argument."),
          ?description = description
        )
          .FromAmong(values |> Array.ofSeq)
        |> HandlerInput.OfArgument

  let modeArg =
    Input.ArgumentMaybe(
      "mode",
      [ "development"; "dev"; "prod"; "production" ],
      "Use Dev or Production dependencies when running, defaults to development"
    )

  let serveCmd =

    let port =
      Input.OptionMaybe<int>(
        [ "--port"; "-p" ],
        "Port where the application starts"
      )

    let host =
      Input.OptionMaybe<string>(
        [ "--host" ],
        "network ip address where the application will run"
      )

    let ssl = Input.OptionMaybe<bool>([ "--ssl" ], "Run dev server with SSL")


    let buildArgs
      (
        mode: string option,
        port: int option,
        host: string option,
        ssl: bool option
      ) : ServeOptions =
      { mode = mode |> Option.map RunConfiguration.FromString
        port = port
        host = host
        ssl = ssl }

    command "serve" {
      description "Starts a development server for single page applications"
      inputs (modeArg, port, host, ssl)
      setHandler (buildArgs >> Handlers.runServe)
    }

  let buildCmd =

    let buildArgs (mode: string option) : BuildOptions =
      { mode = mode |> Option.map RunConfiguration.FromString }

    command "build" {
      description "Builds the SPA application for distribution"
      inputs modeArg
      setHandler (buildArgs >> Handlers.runBuild)
    }

  let initCmd =
    let mode =
      Input.ArgumentWithStrings(
        "mode",
        "simple",
        [ "simple"; "full" ],
        "Selects if we are initializing a project, or perla itself"
      )

    let path =
      Input.ArgumentMaybe<System.IO.DirectoryInfo>(
        "path",
        "Choose what directory to initialize"
      )

    let yes = Input.ArgumentMaybe<bool>("yes", "Accept all of the prompts")

    let fable =
      Input.OptionMaybe<bool>(
        [ "--with-fable"; "-wf" ],
        "The project will use fable"
      )

    let buildArgs
      (
        mode: string,
        path: DirectoryInfo option,
        yes: bool option,
        fable: bool option
      ) : InitOptions =
      { mode = Init.FromString mode
        path =
          path
          |> Option.defaultWith (fun _ -> DirectoryInfo(Path.GetFullPath "./"))
        yes = yes |> Option.defaultValue false
        useFable = fable |> Option.defaultValue false }

    command "init" {
      description "Initialized a given directory or perla itself"
      inputs (mode, path, yes, fable)
      setHandler (buildArgs >> Handlers.runInit)
    }

  let searchPackagesCmd =
    let package =
      Input.Argument(
        "package",
        "The package you want to search for in the skypack api"
      )

    let page =
      Input.ArgumentMaybe(
        "page",
        "change the page number in the search results"
      )

    let buildArgs (package: string, page: int option) : SearchOptions =
      { package = package
        page = page |> Option.defaultValue 1 }

    command "search" {
      description
        "Search a pacakge name in the skypack api, this will bring potential results"

      inputs (package, page)
      setHandler (buildArgs >> Handlers.runSearch)
    }

  let showPackageCmd =
    let package =
      Input.Argument(
        "package",
        "The package you want to search for in the skypack api"
      )

    let buildArgs (package: string) : ShowPackageOptions = { package = package }

    command "show" {
      description
        "Shows information about a package if the name matches an existing one"

      inputs package
      setHandler (buildArgs >> Handlers.runShow)
    }

  let removePackageCmd =
    let package =
      Input.Argument(
        "package",
        "The package you want to search for in the skypack api"
      )

    let alias =
      Input.OptionMaybe(
        [ "--alias"; "-a" ],
        "the alias of the package if you added one"
      )

    let buildArgs
      (
        package: string,
        alias: string option
      ) : RemovePackageOptions =
      { package = package; alias = alias }

    command "remove" {
      description "removes a package from the "

      inputs (package, alias)
      setHandler (buildArgs >> Handlers.runRemove)
    }

  let addPacakgeCmd =
    let package =
      Input.Argument(
        "package",
        "The package you want to search for in the skypack api"
      )

    let version =
      Input.ArgumentMaybe(
        "version",
        "The version of the package you want to use, it defaults to latest"
      )

    let source =
      Input.OptionWithStrings(
        [ "--source"; "-s" ],
        [ "jspm"; "skypack"; "unpkg"; "jsdelivr"; "jspm.system" ],
        "jspm",
        "CDN that will be used to fetch dependencies from"
      )

    let dev =
      Input.OptionMaybe(
        [ "--dev"; "--development"; "-d" ],
        "Adds this dependency to the dev dependencies"
      )

    let alias =
      Input.OptionMaybe(
        [ "--alias"; "-a" ],
        "the alias of the package if you added one"
      )

    let buildArgs
      (
        package: string,
        version: string option,
        source: string,
        dev: bool option,
        alias: string option
      ) : AddPackageOptions =
      { package = package
        version = version |> Option.defaultValue "latest"
        source = Provider.FromString source
        mode =
          dev
          |> Option.map (fun dev ->
            if dev then
              RunConfiguration.Development
            else
              RunConfiguration.Production)
          |> Option.defaultValue RunConfiguration.Production
        alias = alias }

    command "add" {
      description
        "Shows information about a package if the name matches an existing one"

      inputs (package, version, source, dev, alias)
      setHandler (buildArgs >> Handlers.runAdd)
    }

  let listCmd =

    let asNpm =
      Input.OptionMaybe(
        [ "--npm"; "--as-package-json"; "-j" ],
        "Show the packages simlar to npm's package.json"
      )

    let buildArgs (asNpm: bool option) : ListPackagesOptions =
      { format =
          asNpm
          |> Option.map (fun asNpm ->
            if asNpm then
              ListFormat.TextOnly
            else
              ListFormat.HumanReadable)
          |> Option.defaultValue ListFormat.HumanReadable }

    command "list" {
      description
        "Lists the current dependencies in a table or an npm style json string"

      inputs asNpm
      setHandler (buildArgs >> Handlers.runList)
    }

  let restoreCmd =
    let source =
      Input.OptionWithStrings(
        [ "--source"; "-s" ],
        [ "jspm"; "skypack"; "unpkg"; "jsdelivr"; "jspm.system" ],
        "jspm",
        "CDN that will be used to fetch dependencies from"
      )

    let mode =
      Input.OptionWithStrings(
        [ "--mode"; "-m" ],
        [ "dev"; "development"; "prod"; "production" ],
        "production",
        "Restore Dependencies based on the mode to run"
      )

    let buildArgs (source: string, mode: string) : RestoreOptions =
      { source = Provider.FromString source
        mode = RunConfiguration.FromString mode }

    command "restore" {
      description
        "Restore the import map based on the selected mode, defaults to production"

      inputs (source, mode)
      setHandler (buildArgs >> Handlers.runRestore)
    }

  let addTemplateCmd, updateTemplateCmd =
    let repoName =
      Input.Argument(
        "templateRepositoryName",
        "The User/repository name combination"
      )

    let branch =
      Input.Argument("banch", "Whch branch to pick the template from")

    let yes = Input.OptionMaybe([ "--yes"; "--continue"; "-y" ], "skip prompts")

    let buildArgs
      (
        name: string,
        branch: string,
        yes: bool option
      ) : TemplateRepositoryOptions =
      { fullRepositoryName = name
        branch = branch
        yes = yes |> Option.defaultValue false }

    let add =
      command "templates:add" {
        description "Adds a new template from a particular repository"
        inputs (repoName, branch, yes)
        setHandler (buildArgs >> Handlers.runAddTemplate)
      }

    let update =
      command "templates:update" {
        description "Updates an existing template in the templates database"
        inputs (repoName, branch, yes)
        setHandler (buildArgs >> Handlers.runUpdateTemplate)
      }

    add, update

  let listTemplatesCmd =
    let display =
      Input.ArgumentWithStrings("format", "table", [ "table"; "simple" ])

    let buildArgs (format: string) : ListTemplatesOptions =
      let toFormat =
        match format.ToLowerInvariant() with
        | "table" -> ListFormat.HumanReadable
        | _ -> ListFormat.TextOnly

      { format = toFormat }

    command "templates:list" {
      inputs display
      setHandler (buildArgs >> Handlers.runListTemplates)
    }

  let removeTemplateCmd =
    let repoName =
      Input.Argument(
        "templateRepositoryName",
        "The User/repository name combination"
      )

    command "templates:delete" {
      description "Removes a template from the templates database"
      inputs (repoName)
      setHandler Handlers.runRemoveTemplate
    }

  let newProjectCmd =
    let name = Input.Argument("name", "Name of the new project")

    let templateName =
      Input.Argument(
        "templateName",
        "repository/directory combination of the template name, or the full name in case of name conflicts username/repository/directory"
      )

    let buildArgs (name: string, template: string) : ProjectOptions =
      { projectName = name
        templateName = template }

    command "new" {
      description
        "Creates a new project based on the selected template if it exists"

      inputs (name, templateName)
      setHandler (buildArgs >> Handlers.runNew)
    }

  let versionCmd =
    let isSemver =
      Input.OptionMaybe([ "--semver" ], "Gets the version of the application")

    command "--version" {
      description "Shows the full or semver version of the application"
      inputs isSemver

      setHandler (
        (fun isSemver -> isSemver |> Option.defaultValue true)
        >> Handlers.runVersion
      )
    }
