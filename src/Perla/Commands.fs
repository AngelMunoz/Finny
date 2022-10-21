namespace Perla

open System
open System.IO

open System.Threading
open System.Threading.Tasks
open Spectre.Console

open FSharp.Control
open FSharp.Control.Reactive
open FSharp.UMX
open FsToolkit.ErrorHandling

open AngleSharp

open Perla
open Perla.Types
open Perla.Units
open Perla.Server
open Perla.Build
open Perla.Logger
open Perla.FileSystem
open Perla.VirtualFs
open Perla.Esbuild
open Perla.Fable
open Perla.Json
open Perla.Scaffolding
open Perla.Configuration.Types
open Perla.Configuration

open Perla.Plugins.Extensibility

open Perla.PackageManager
open Perla.PackageManager.Types



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

  type BuildOptions =
    { mode: RunConfiguration option
      disablePreloads: bool }

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
      version: string option
      source: Provider option
      mode: RunConfiguration option
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
    { source: Provider option
      mode: RunConfiguration option }

  type Init with

    static member FromString(value: string) =
      match value.ToLowerInvariant() with
      | "full" -> Init.Full
      | "simple"
      | _ -> Init.Simple

open CliOptions


[<RequireQualifiedAccess>]
module Handlers =
  open AngleSharp.Html.Parser
  open Zio.FileSystems
  open Zio

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
          Logger.log
            $"Successfully added {template.fullName} at {template.path}"

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
          do! FileSystem.SetupEsbuild(UMX.tag Constants.Esbuild_Version)

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
      do! Dependencies.Search(options.package, options.page)
      return 0
    }

  let runShow (options: ShowPackageOptions) =
    task {
      do! Dependencies.Show(options.package)
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

  let runNew (opts: ProjectOptions) =
    Logger.log "Creating new project..."
    let user, template, child = getTemplateAndChild opts.templateName

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
        |> Option.map Scaffolding.getConfigurationFromScript
        |> Option.flatten

      Logger.log $"Creating structure..."

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
        return Templates.Delete repo.fullName
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

  let runRemove (options: RemovePackageOptions) =
    task {
      let name = options.package
      Logger.log ($"Removing: [red]{name}[/]", escape = false)
      let config = Configuration.CurrentConfig

      let map = FileSystem.ImportMap()

      let dependencies, devDependencies =
        let deps, devDeps =
          Dependencies.LocateDependenciesFromMapAndConfig(
            map,
            { config with
                dependencies =
                  config.dependencies |> Seq.filter (fun d -> d.name <> name)
                devDependencies =
                  config.devDependencies |> Seq.filter (fun d -> d.name <> name) }
          )

        deps, devDeps

      Configuration.WriteFieldsToFile(
        [ PerlaWritableField.Dependencies dependencies
          PerlaWritableField.DevDependencies devDependencies ]
      )

      let packages =
        match config.runConfiguration with
        | RunConfiguration.Production ->
          dependencies |> Seq.map (fun f -> f.AsVersionedString)
        | RunConfiguration.Development ->
          [ yield! dependencies; yield! devDependencies ]
          |> Seq.map (fun f -> f.AsVersionedString)

      match!
        Dependencies.Restore(
          packages,
          Provider.Jspm,
          runConfig = config.runConfiguration
        )
      with
      | Ok map ->
        FileSystem.WriteImportMap(map) |> ignore
        return 0
      | Error err ->
        Logger.log $"[bold red]{err}[/]"
        return 1
    }

  let runAdd (options: AddPackageOptions) =
    task {
      Configuration.UpdateFromCliArgs(
        ?runConfig = options.mode,
        ?provider = options.source
      )

      let config = Configuration.CurrentConfig
      let package, packageVersion = parsePackageName options.package

      match options.alias with
      | Some _ ->
        Logger.log (
          $"[bold yellow]Aliases management will change in the future, they will be ignored if this warning appears[/]",
          escape = false
        )
      | None -> ()

      let version =
        match packageVersion with
        | Some version -> $"@{version}"
        | None -> ""

      let importMap = FileSystem.ImportMap()
      Logger.log "Updating Import Map..."

      let! map =
        Logger.spinner (
          $"Adding: [bold yellow]{package}{version}[/]",
          Dependencies.Add(
            $"{package}{version}",
            importMap,
            provider = config.provider,
            runConfig = config.runConfiguration
          )
        )

      match map with
      | Ok map ->
        let dependencies, devDependencies =
          let deps, devDeps =
            Dependencies.LocateDependenciesFromMapAndConfig(
              map,
              { config with
                  dependencies =
                    [ yield! config.dependencies
                      { name = package
                        version = packageVersion
                        alias = options.alias } ] }
            )

          PerlaWritableField.Dependencies deps,
          PerlaWritableField.DevDependencies devDeps

        Configuration.WriteFieldsToFile([ dependencies; devDependencies ])

        FileSystem.WriteImportMap(map) |> ignore
        return 0
      | Error err ->
        Logger.log ($"[bold red]{err}[/]", escape = false)
        return 1
    }

  let runRestore (options: RestoreOptions) =
    task {
      Configuration.UpdateFromCliArgs(
        ?runConfig = options.mode,
        ?provider = options.source
      )

      let config = Configuration.CurrentConfig

      Logger.log "Regenerating import map..."

      let packages =
        [ yield! config.dependencies
          match config.runConfiguration with
          | RunConfiguration.Development -> yield! config.devDependencies
          | RunConfiguration.Production -> () ]
        |> List.map (fun d -> d.AsVersionedString)
        // deduplicate repeated strings
        |> set

      match!
        Logger.spinner (
          "Fetching dependencies...",
          Dependencies.Restore(packages, provider = config.provider)
        )
      with
      | Ok response -> FileSystem.WriteImportMap(response) |> ignore
      | Error err ->
        Logger.log $"An error happened restoring the import map:\n{err}"

      return 0
    }

  let runBuild (args: BuildOptions) =
    task {

      Configuration.UpdateFromCliArgs(?runConfig = args.mode)
      let config = Configuration.CurrentConfig
      use cts = new CancellationTokenSource()

      Console.CancelKeyPress.Add(fun _ ->
        Logger.log "Got it, see you around!..."
        cts.Cancel())

      match config.fable with
      | Some fable ->
        do!
          Logger.spinner (
            "Running Fable...",
            Fable.Start(fable, false, cancellationToken = cts.Token)
          )
          :> Task
      | None ->
        Logger.log (
          "No Fable configuration provided, skipping fable",
          target = PrefixKind.Build
        )

      if not <| File.Exists($"{FileSystem.EsbuildBinaryPath}") then
        do! FileSystem.SetupEsbuild(config.esbuild.version, cts.Token)

      let outDir = UMX.untag config.build.outDir

      try
        Directory.Delete(outDir, true)
        Directory.CreateDirectory(outDir) |> ignore
      with _ ->
        ()

      Plugins.LoadPlugins(config.esbuild)

      do!
        Logger.spinner (
          "Mounting Virtual File System",
          VirtualFileSystem.Mount config
        )

      let tempDirectory = VirtualFileSystem.CopyToDisk() |> UMX.tag<SystemPath>

      Logger.log (
        $"Copying Processed files to {tempDirectory}",
        target = PrefixKind.Build
      )

      let externals = Build.GetExternals(config)

      let index = FileSystem.IndexFile(config.index)

      use browserCtx = new BrowsingContext()

      let parser = browserCtx.GetService<IHtmlParser>()

      let document = parser.ParseDocument index

      use fs = new PhysicalFileSystem()

      let tmp = UMX.untag tempDirectory |> fs.ConvertPathFromInternal

      let css, js = Build.GetEntryPoints(document)

      let runEsbuild =
        Task.WhenAll(
          backgroundTask {
            for css in css do
              let path =
                UPath.Combine(tmp, UMX.untag css) |> fs.ConvertPathToInternal

              let targetPath =
                Path.Combine(UMX.untag config.build.outDir, UMX.untag css)
                |> Path.GetDirectoryName
                |> UMX.tag<SystemPath>

              let tsk =
                Esbuild
                  .ProcessCss(path, config.esbuild, targetPath)
                  .ExecuteAsync(cts.Token)

              do! tsk.Task :> Task
          },

          backgroundTask {
            for js in js do
              let path =
                UPath.Combine(tmp, UMX.untag js) |> fs.ConvertPathToInternal

              let targetPath =
                Path.Combine(UMX.untag config.build.outDir, UMX.untag js)
                |> Path.GetDirectoryName
                |> UMX.tag<SystemPath>

              let tsk =
                Esbuild
                  .ProcessJS(path, config.esbuild, targetPath, externals)
                  .ExecuteAsync(cts.Token)

              do! tsk.Task :> Task
          }
        )

      do! Logger.spinner ("Transpiling CSS and JS Files", runEsbuild) :> Task

      let css =
        [ yield! css
          yield!
            js
            |> Seq.map (fun p ->
              Path.ChangeExtension(UMX.untag p, ".css") |> UMX.tag) ]

      let! indexContent =
        task {
          let dependencies =
            match config.runConfiguration with
            | RunConfiguration.Production ->
              config.dependencies |> Seq.map (fun d -> d.AsVersionedString)
            | RunConfiguration.Development ->
              [ yield! config.dependencies; yield! config.devDependencies ]
              |> Seq.map (fun d -> d.AsVersionedString)

          match!
            Logger.spinner (
              "Resolving Static dependencies and import map...",
              Dependencies.GetMapAndDependencies(dependencies, config.provider)
            )
          with
          | Ok (deps, map) ->
            FileSystem.WriteImportMap(map) |> ignore
            let deps = if args.disablePreloads then Seq.empty else deps
            return Build.GetIndexFile(document, css, js, map, deps)
          | Error err ->
            Logger.log
              $"We were unable to update static dependencies and import map: {err}, falling back to the map in disk"

            let map = FileSystem.ImportMap()
            return Build.GetIndexFile(document, css, js, map)
        }

      let outDir =
        config.build.outDir
        |> UMX.untag
        |> Path.GetFullPath
        |> fs.ConvertPathFromInternal

      fs.WriteAllText(UPath.Combine(outDir, "index.html"), indexContent)

      // copy any glob files
      Build.CopyGlobs(config.build)
      // copy any root files
      fs.EnumerateFileEntries(tmp, "*.*", SearchOption.TopDirectoryOnly)
      |> Seq.iter (fun file ->
        file.CopyTo(UPath.Combine(outDir, file.Name), true) |> ignore)

      Logger.log $"Cleaning up temp dir {tempDirectory}"

      try
        Directory.Delete(UMX.untag tempDirectory, true)
      with ex ->
        Logger.log ($"Failed to delete {tempDirectory}", ex = ex)

      return 0
    }

  let runServe (options: ServeOptions) =
    task {
      let cliArgs =
        [ match options.port with
          | Some port -> DevServerField.Port port
          | None -> ()
          match options.host with
          | Some host -> DevServerField.Host host
          | None -> ()
          match options.ssl with
          | Some ssl -> DevServerField.UseSSl ssl
          | None -> () ]

      Configuration.UpdateFromCliArgs(
        ?runConfig = options.mode,
        serverOptions = cliArgs
      )

      let config = Configuration.CurrentConfig

      use cts = new CancellationTokenSource()

      Console.CancelKeyPress.Add(fun _ ->
        Logger.log "Got it, see you around!..."
        cts.Cancel())

      do!
        backgroundTask {
          do! FileSystem.SetupEsbuild(config.esbuild.version, cts.Token)
        }

      Plugins.LoadPlugins(config.esbuild)

      do! VirtualFileSystem.Mount(config)

      let fileChangeEvents =
        VirtualFileSystem.GetFileChangeStream config.mountDirectories
        |> VirtualFileSystem.ApplyVirtualOperations

      // TODO: Grab these from esbuild
      let compilerErrors = Observable.empty

      let app = Server.GetServerApp(config, fileChangeEvents, compilerErrors)
      do! app.StartAsync(cts.Token)

      match config.fable with
      | Some fable ->
        backgroundTask {
          let logger msg =
            Logger.log msg

            if msg.ToLowerInvariant().Contains("watching") then
              let urls =
                app.Urls
                |> Seq.fold (fun current next -> $"{current}\n{next}") ""

              Logger.log $"Server Ready at {urls}"

              app.StartAsync(cts.Token) |> Async.AwaitTask |> Async.Start

          do!
            Fable.Start(fable, true, logger, cancellationToken = cts.Token)
            :> Task
        }
        |> ignore
      | None -> ()


      Console.CancelKeyPress.Add(fun _ ->
        app.StopAsync() |> Async.AwaitTask |> Async.RunSynchronously)

      while not cts.IsCancellationRequested do
        do! Async.Sleep(TimeSpan.FromSeconds(1.))

      return 0
    }


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
          ?values: string seq,
          ?defaultValue: string,
          ?description
        ) =
        let option =
          Opt<string option>(
            aliases |> Array.ofSeq,
            getDefaultValue = (fun _ -> defaultValue),
            ?description = description
          )

        match values with
        | Some values -> option.FromAmong(values |> Array.ofSeq)
        | None -> option
        |> HandlerInput.OfOption

      static member ArgumentWithStrings
        (
          name: string,
          ?values: string seq,
          ?defaultValue: string,
          ?description
        ) =
        let arg =
          Arg<string option>(
            name,
            getDefaultValue = (fun _ -> defaultValue),
            ?description = description
          )

        match values with
        | Some values -> arg.FromAmong(values |> Array.ofSeq)
        | None -> arg
        |> HandlerInput.OfArgument

      static member ArgumentMaybe
        (
          name: string,
          ?values: string seq,
          ?description
        ) =
        let arg =
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

        match values with
        | Some values -> arg.FromAmong(values |> Array.ofSeq)
        | None -> arg
        |> HandlerInput.OfArgument

  let runAsDev =
    Input.OptionMaybe(
      [ "--development"; "-d"; "--dev" ],
      "Use Dev dependencies when running, defaults to false"
    )

  let Build =
    let disablePreloads =
      Input.OptionMaybe(
        [ "-dpl"; "--disable-preload-links" ],
        "disables adding modulepreload links in the final build"
      )

    let buildArgs
      (
        runAsDev: bool option,
        disablePreloads: bool option
      ) : BuildOptions =
      { mode =
          runAsDev
          |> Option.map (fun runAsDev ->
            match runAsDev with
            | true -> RunConfiguration.Development
            | false -> RunConfiguration.Production)
        disablePreloads = defaultArg disablePreloads false }

    command "build" {
      description "Builds the SPA application for distribution"
      inputs (runAsDev, disablePreloads)
      setHandler (buildArgs >> Handlers.runBuild)
    }

  let Serve =

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
        mode: bool option,
        port: int option,
        host: string option,
        ssl: bool option
      ) : ServeOptions =
      { mode =
          mode
          |> Option.map (fun runAsDev ->
            match runAsDev with
            | true -> RunConfiguration.Development
            | false -> RunConfiguration.Production)
        port = port
        host = host
        ssl = ssl }

    command "serve" {
      inputs (runAsDev, port, host, ssl)
      setHandler (buildArgs >> Handlers.runServe)
    }

  let Init =
    let mode =
      Input.ArgumentWithStrings(
        "mode",
        [ "simple"; "full" ],
        description =
          "Selects if we are initializing a project, or perla itself"
      )

    let path =
      Input.ArgumentMaybe<DirectoryInfo>(
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
        mode: string option,
        path: DirectoryInfo option,
        yes: bool option,
        fable: bool option
      ) : InitOptions =
      { mode =
          mode |> Option.map Init.FromString |> Option.defaultValue Init.Simple
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

  let SearchPackages =
    let package =
      Input.Argument(
        "package",
        "The package you want to search for in the Skypack api"
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
        "Search a package name in the Skypack api, this will bring potential results"

      inputs (package, page)
      setHandler (buildArgs >> Handlers.runSearch)
    }

  let ShowPackage =
    let package =
      Input.Argument(
        "package",
        "The package you want to search for in the Skypack api"
      )

    let buildArgs (package: string) : ShowPackageOptions = { package = package }

    command "show" {
      description
        "Shows information about a package if the name matches an existing one"

      inputs package
      setHandler (buildArgs >> Handlers.runShow)
    }

  let RemovePackage =
    let package =
      Input.Argument(
        "package",
        "The package you want to search for in the Skypack api"
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

  let AddPackage =
    let package =
      Input.Argument(
        "package",
        "The package you want to search for in the skypack api"
      )

    let version =
      let opt =
        Aliases.Opt<string option>(
          [| "-v"; "--version" |],
          parseArgument =
            (fun arg ->
              arg.Tokens
              |> Seq.tryHead
              |> Option.map (fun token -> token.Value |> Option.ofObj)
              |> Option.flatten)
        )

      opt |> HandlerInput.OfOption

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
        source: string option,
        dev: bool option,
        alias: string option
      ) : AddPackageOptions =
      { package = package
        version = version
        source = source |> Option.map Provider.FromString
        mode =
          dev
          |> Option.map (fun dev ->
            if dev then
              RunConfiguration.Development
            else
              RunConfiguration.Production)
        alias = alias }

    command "add" {
      description
        "Shows information about a package if the name matches an existing one"

      inputs (package, version, source, dev, alias)
      setHandler (buildArgs >> Handlers.runAdd)
    }

  let ListDependencies =

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

  let Restore =
    let source =
      Input.OptionWithStrings(
        [ "--source"; "-s" ],
        [ "jspm"; "skypack"; "unpkg"; "jsdelivr"; "jspm.system" ],
        description = "CDN that will be used to fetch dependencies from"
      )

    let mode =
      Input.OptionWithStrings(
        [ "--mode"; "-m" ],
        [ "dev"; "development"; "prod"; "production" ],
        description = "Restore Dependencies based on the mode to run"
      )

    let buildArgs
      (
        source: string option,
        mode: string option
      ) : RestoreOptions =
      { source = source |> Option.map Provider.FromString
        mode = mode |> Option.map RunConfiguration.FromString }

    command "restore" {
      description
        "Restore the import map based on the selected mode, defaults to production"

      inputs (source, mode)
      setHandler (buildArgs >> Handlers.runRestore)
    }

  let AddTemplate, UpdateTemplate =
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

  let ListTemplates =
    let display =
      Input.ArgumentWithStrings(
        "format",
        [ "table"; "simple" ],
        description =
          "The chosen format to display the existing perla templates"
      )

    let buildArgs (format: string option) : ListTemplatesOptions =
      let toFormat (format: string) =
        match format.ToLowerInvariant() with
        | "table" -> ListFormat.HumanReadable
        | _ -> ListFormat.TextOnly

      { format =
          format
          |> Option.map toFormat
          |> Option.defaultValue ListFormat.HumanReadable }

    command "templates:list" {
      inputs display
      setHandler (buildArgs >> Handlers.runListTemplates)
    }

  let RemoveTemplate =
    let repoName =
      Input.Argument(
        "templateRepositoryName",
        "The User/repository name combination"
      )

    command "templates:delete" {
      description "Removes a template from the templates database"
      inputs repoName
      setHandler Handlers.runRemoveTemplate
    }

  let NewProject =
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
