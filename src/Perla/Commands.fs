namespace Perla

open System
open System.CommandLine.Invocation
open System.IO

open System.Threading
open System.Threading.Tasks
open System.Reactive.Threading.Tasks
open Microsoft.Playwright
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

open Perla.Testing


module CliOptions =
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

  type SetupOptions = { skipPrompts: bool }

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
      branch: string
      yes: bool }

  type ProjectOptions =
    { projectName: string
      templateName: string }

  type RestoreOptions =
    { source: Provider option
      mode: RunConfiguration option }

  type TestingOptions =
    { files: string seq
      browsers: Browser seq
      headless: bool
      watch: bool }

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
      $"Download and extracting template {template.ToFullNameWithBranch}"

    Templates.Update({ template with branch = branch })

  let private addTemplate
    (user: string)
    (repository: string)
    (branch: string)
    (context: StatusContext)
    =
    context.Status <-
      $"Download and extracting template {user}/{repository}:{branch}"

    Templates.Add(user, repository, branch)

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
            let basePath =
              FileSystem.PathForTemplate(
                result.username,
                result.repository,
                result.branch
              )

            DirectoryInfo(basePath).EnumerateDirectories()
            |> Seq.map (fun d -> $"[green]{d.Name}[/]")

          String.Join("\n", names) |> Markup

        let path =
          TextPath(
            UMX.untag result.path,
            LeafStyle = Style(Color.Green),
            StemStyle = Style(Color.Yellow),
            SeparatorStyle = Style(Color.Blue)
          )

        table.AddRow(
          Markup($"[yellow]{result.ToFullName}[/]"),
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
        Logger.log
          $"[bold green]{result.ToFullName}[/]:[bold yellow]{result.branch}[/]"

        let path = TextPath(UMX.untag result.path).StemColor(Color.Blue)
        AnsiConsole.Write path

      0

  let runAddTemplate (opts: TemplateRepositoryOptions) =
    task {
      let autoContinue = opts.yes

      let mutable chosen = parseFullRepositoryName opts.fullRepositoryName

      while chosen |> Option.isNone do
        chosen <-
          AnsiConsole.Ask(
            "that doesn't feel right, please tell us the  username/repository:branch github repository to look for templates"
          )
          |> parseFullRepositoryName

        match chosen with
        | None -> ()
        | parsed -> chosen <- parsed

      let username, repository, branch = chosen.Value

      let template =
        TemplateSearchKind.FullName(username, repository) |> Templates.FindOne

      match template, autoContinue with
      | Some template, true ->
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
      | Some template, false ->
        let prompt =
          SelectionPrompt<string>(
            Title =
              $"\"{opts.fullRepositoryName}\" already exists, Do you want to update it?"
          )
            .AddChoices([ "Yes"; "No" ])

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
          Logger.log $"Template {template.ToFullNameWithBranch} was not updated"

          return 0
      | None, _ ->

        let! tplId =
          Logger.spinner (
            "Adding new templates",
            addTemplate username repository opts.branch
          )

        match Templates.FindOne(TemplateSearchKind.Id tplId) with
        | Some template ->
          Logger.log $"Successfully added {template.ToFullNameWithBranch}"

          let path =
            TextPath(
              UMX.untag template.path,
              LeafStyle = Style(Color.Green),
              StemStyle = Style(Color.Yellow),
              SeparatorStyle = Style(Color.Blue)
            )

          AnsiConsole.Write("Template at path: ")
          AnsiConsole.Write(path)
          return 0
        | None ->
          Logger.log
            $"Template may have been downloaded but for some reason we can't find it, this is likely a bug"

          return 1

    }

  let runNew (opts: ProjectOptions) =
    Logger.log "Creating new project..."
    let user, template, child = getTemplateAndChild opts.templateName

    option {
      let! template =
        match user, child with
        | Some user, _ ->
          Templates.FindOne(TemplateSearchKind.FullName(user, template))
        | None, _ -> Templates.FindOne(TemplateSearchKind.Repository template)

      Logger.log (
        $"Using [bold yellow]{template.ToFullNameWithBranch}[/]",
        escape = false
      )

      let templatePath =
        FileSystem.PathForTemplate(
          template.username,
          template.repository,
          template.branch,
          ?tplName = child
        )

      let targetPath = opts.projectName

      let content =
        FileSystem.GetTemplateScriptContent(
          template.username,
          template.repository,
          template.branch,
          ?tplName = child
        )
        |> Option.map Scaffolding.getConfigurationFromScript
        |> Option.flatten

      Logger.log $"Creating structure..."

      FileSystem.WriteTplRepositoryToDisk(
        UMX.tag<SystemPath> templatePath,
        UMX.tag<UserPath> targetPath,
        ?payload = content
      )

      Logger.log (
        $"Your project is ready at [bold green]{targetPath}[/]",
        escape = false
      )

      Logger.log ($"cd {targetPath}", escape = false)
      Logger.log ("[bold green]perla serve[/]", escape = false)

      return 0
    }
    |> function
      | None ->
        Logger.log $"Template [{opts.templateName}] was not found"
        1
      | Some n -> n

  let runInit (options: SetupOptions) =
    task {
      Logger.log "Perla will set up the following resources:"
      Logger.log "- Esbuild"
      Logger.log "- Default Templates"

      Logger.log
        "After that you should be able to run 'perla build' or 'perla new'"

      do! FileSystem.SetupEsbuild(UMX.tag Constants.Esbuild_Version)
      Logger.log ("[bold green]esbuild[/] has been setup!", escape = false)


      if options.skipPrompts then
        let username, repository, branch =
          PerlaTemplateRepository.DefaultTemplatesRepository

        match
          TemplateSearchKind.FullName(username, repository) |> Templates.FindOne
        with
        | Some template ->
          Logger.log
            $"{template.ToFullName} is already set up, updating from {template.branch}"


          let! result = Templates.Update(template)

          if not result then
            Logger.log (
              "We were unable to update the existing [bold red]templates[/].",
              escape = false
            )

            return 1
          else
            runListTemplates { format = ListFormat.HumanReadable } |> ignore

            Logger.log (
              "[bold yellow]perla[/] [bold blue]new [/] [bold green]perla-templates/<TEMPLATE_NAME>[/] [bold blue] <PROJECT_NAME>[/]",
              escape = false
            )

            Logger.log "Feel  free to create a new perla project"
            return 0
        | None ->
          let! _ =
            Logger.spinner (
              "Adding default templates",
              addTemplate username repository branch
            )

          runListTemplates { format = ListFormat.HumanReadable } |> ignore

          Logger.log (
            "[bold yellow]perla[/] [bold blue]new [/] [bold green]perla-templates/<TEMPLATE_NAME>[/] [bold blue] <PROJECT_NAME>[/]",
            escape = false
          )

          return 0
      else
        let chosen =
          AnsiConsole.Ask(
            "Tell us the Username/repository:branch github repository to look for templates",
            $"{Constants.Default_Templates_Repository}:main"
          )

        let mutable chosen = parseFullRepositoryName chosen

        while chosen |> Option.isNone do
          chosen <-
            AnsiConsole.Ask(
              "Tell us the Username/repository:branch github repository to look for templates",
              $"{Constants.Default_Templates_Repository}:main"
            )
            |> parseFullRepositoryName

          match chosen with
          | None -> ()
          | parsed -> chosen <- parsed

        let username, repository, branch = chosen.Value

        let! tplId = Templates.Add(username, repository, branch)

        match Templates.FindOne(TemplateSearchKind.Id tplId) with
        | Some template ->
          Logger.log
            $"Successfully added {template.ToFullNameWithBranch} at {template.path}"

          runListTemplates { format = ListFormat.HumanReadable } |> ignore
          return 0
        | None ->
          Logger.log
            $"Template may have been downloaded but for some reason we can't find it, this is likely a bug"

          return 1

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

        let prodTable =
          dependencyTable (config.dependencies, "Production Dependencies")

        let devTable =
          dependencyTable (config.devDependencies, "Development Dependencies")

        AnsiConsole.Write(prodTable)
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

  let runRemoveTemplate (name: string) =
    let deleteOperation =
      option {
        let! username, repository, _ = parseFullRepositoryName name

        return
          Templates.Delete(TemplateSearchKind.FullName(username, repository))
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
    task {
      match parseFullRepositoryName opts.fullRepositoryName with
      | Some (username, repository, branch) ->
        match
          Templates.FindOne(TemplateSearchKind.FullName(username, repository))
        with
        | Some template ->
          match!
            Logger.spinner ("Updating Template", updateTemplate template branch)
          with
          | true ->
            Logger.log (
              $"[bold green]{opts.fullRepositoryName}[/] - [yellow]{branch}[/] updated correctly",
              escape = false
            )

            return 0
          | _ ->
            Logger.log (
              $"[bold red]{opts.fullRepositoryName}[/] - [yellow]{branch}[/] failed to update",
              escape = false
            )

            return 1
        | None ->
          Logger.log (
            $"[bold red]{opts.fullRepositoryName}[/] - [yellow]{branch}[/] failed to update",
            escape = false
          )

          return 1
      | None ->
        Logger.log (
          $"[bold red]{opts.fullRepositoryName}[/] Was not found in the templates",
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

  let runBuild (cancel: CancellationToken, args: BuildOptions) =
    task {
      Configuration.UpdateFromCliArgs(?runConfig = args.mode)
      let config = Configuration.CurrentConfig

      match config.fable with
      | Some fable ->
        do!
          backgroundTask {
            do! Fable.Start(fable, false, cancellationToken = cancel) :> Task
          }
      | None ->
        Logger.log (
          "No Fable configuration provided, skipping fable",
          target = PrefixKind.Build
        )

      if not <| File.Exists($"{FileSystem.EsbuildBinaryPath}") then
        do! FileSystem.SetupEsbuild(config.esbuild.version, cancel)

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
                  .ExecuteAsync(cancel)

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
                  .ExecuteAsync(cancel)

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

  let private getFableLogger (config: PerlaConfig) =
    fun msg ->
      Logger.log msg
      let msg = msg.ToLowerInvariant()

      if msg.Contains("watching") || msg.Contains("compilation finished") then
        let config = config.devServer

        let http, https =
          Server.GetServerURLs config.host config.port config.useSSL

        Logger.log $"Server Ready at:"
        Logger.log $"{http}\n{https}"

  let runServe (cancel: CancellationToken, options: ServeOptions) =
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

      match config.fable with
      | Some fable ->
        let logger = getFableLogger config

        backgroundTask {
          do!
            Fable.Start(fable, true, logger, cancellationToken = cancel) :> Task
        }
        |> ignore
      | None -> ()

      do!
        backgroundTask {
          do! FileSystem.SetupEsbuild(config.esbuild.version, cancel)
        }

      Plugins.LoadPlugins(config.esbuild)

      do! VirtualFileSystem.Mount(config)


      let fileChangeEvents =
        VirtualFileSystem.GetFileChangeStream config.mountDirectories
        |> VirtualFileSystem.ApplyVirtualOperations

      // TODO: Grab these from esbuild
      let compilerErrors = Observable.empty

      let app = Server.GetServerApp(config, fileChangeEvents, compilerErrors)

      do! app.StartAsync(cancel)

      Console.CancelKeyPress.Add(fun _ ->
        app.StopAsync() |> Async.AwaitTask |> Async.RunSynchronously)

      while not cancel.IsCancellationRequested do
        do! Async.Sleep(TimeSpan.FromSeconds(1.))

      return 0
    }


  let runTesting (cancel: CancellationToken, options: TestingOptions) =
    task {
      let config = Configuration.CurrentConfig
      Testing.SetupPlaywright()
      let logger = getFableLogger config

      match config.fable, options.watch with
      | Some fable, true ->
        backgroundTask {
          do!
            Fable.Start(
              fable,
              options.watch,
              logger,
              cancellationToken = cancel
            )
            :> Task
        }
        |> ignore
      | Some fable, false ->
        do!
          backgroundTask {
            return!
              Fable.Start(
                fable,
                options.watch,
                logger,
                cancellationToken = cancel
              )
              :> Task
          }
      | None, _ -> ()

      do! FileSystem.SetupEsbuild(config.esbuild.version, cancel)

      let! dependencies =
        Logger.spinner (
          "Resolving Static dependencies and import map...",
          Dependencies.GetMapAndDependencies(
            [ yield! config.dependencies; yield! config.devDependencies ]
            |> Seq.map (fun d -> d.AsVersionedString),
            config.provider
          )
        )

      let dependencies =
        dependencies
        |> Result.defaultWith (fun _ -> (Seq.empty, FileSystem.ImportMap()))

      Plugins.LoadPlugins(config.esbuild)

      let mountedDirs =
        config.mountDirectories
        |> Map.add (UMX.tag<ServerUrl> "/tests") (UMX.tag<UserPath> "./tests")

      do!
        VirtualFileSystem.Mount({ config with mountDirectories = mountedDirs })

      let fileChangeEvents =
        VirtualFileSystem.GetFileChangeStream config.mountDirectories
        |> VirtualFileSystem.ApplyVirtualOperations

      // TODO: Grab these from esbuild
      let compilerErrors = Observable.empty

      let config =
        { config with
            devServer = { config.devServer with liveReload = options.watch } }

      let events = Subject<TestEvent>.broadcast

      let app =
        Server.GetTestingApp(
          config,
          dependencies,
          events,
          fileChangeEvents,
          compilerErrors,
          options.files
        )

      do! app.StartAsync(cancel)

      use! pl = Playwright.CreateAsync()

      use! browser =
        Testing.GetBrowser(options.browsers |> Seq.head, options.headless, pl)

      let! page = browser.NewPageAsync()

      let http, _ =
        Server.GetServerURLs
          config.devServer.host
          config.devServer.port
          config.devServer.useSSL

      do! page.GotoAsync(http) :> Task


      page.Console
      |> Observable.add (fun e ->
        match e.Type with
        | Debug -> Logger.log $"[bold blue]Browser:[/] {e.Text.EscapeMarkup()}"
        | Info ->
          Logger.log
            $"[bold cyan]Browser:[/] [bold cyan]{e.Text.EscapeMarkup()}[/]"
        | Err -> Logger.log $"[bold red]Browser:[/]{e.Text.EscapeMarkup()}"
        | Warning ->
          Logger.log $"[bold orange]Browser:[/]{e.Text.EscapeMarkup()}"
        | Clear ->
          Logger.log
            $"[yellow]Browser:[/] Browser Console cleared at: [link]{e.Location}[/]"
        | _ -> Logger.log $"[bold yellow]Browser:[/] {e.Text.EscapeMarkup()}"

        AnsiConsole.Write(
          Rule(
            $"[dim blue]{e.Location}[/]",
            Style = Style.Parse("dim"),
            Alignment = Justify.Right
          )
        ))

      if not config.devServer.liveReload then
        events
        |> Observable.toEnumerable
        |> Testing.BuildReport
        |> Testing.Print.Report

        return 0
      else
        use _ = events |> Testing.PrintReportLive

        while not cancel.IsCancellationRequested do
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

      static member OptionWithMultipleStrings
        (
          aliases: string seq,
          ?values: string seq,
          ?description
        ) =
        let option =
          Opt<string[]>(
            aliases |> Array.ofSeq,
            getDefaultValue = (fun _ -> Array.empty),
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
        context: InvocationContext,
        runAsDev: bool option,
        disablePreloads: bool option
      ) =
      (context.GetCancellationToken(),
       { mode =
           runAsDev
           |> Option.map (fun runAsDev ->
             match runAsDev with
             | true -> RunConfiguration.Development
             | false -> RunConfiguration.Production)
         disablePreloads = defaultArg disablePreloads false })

    command "build" {
      description "Builds the SPA application for distribution"
      inputs (Input.Context(), runAsDev, disablePreloads)
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
        context: InvocationContext,
        mode: bool option,
        port: int option,
        host: string option,
        ssl: bool option
      ) =
      (context.GetCancellationToken(),
       { mode =
           mode
           |> Option.map (fun runAsDev ->
             match runAsDev with
             | true -> RunConfiguration.Development
             | false -> RunConfiguration.Production)
         port = port
         host = host
         ssl = ssl })

    command "serve" {
      inputs (Input.Context(), runAsDev, port, host, ssl)
      setHandler (buildArgs >> Handlers.runServe)
    }

  let Init =
    let skipPrompts =
      Input.OptionMaybe<bool>(
        [ "--skip-prompts"; "-y"; "--yes"; "-sp" ],
        "Skip prompts"
      )

    let buildArgs (yes: bool option) : SetupOptions =
      { skipPrompts = yes |> Option.defaultValue false }

    command "init" {
      description "Initialized a given directory or perla itself"
      inputs skipPrompts
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

  let Test =
    let files =
      Input.Option(
        [ "--tests"; "-t" ],
        None,
        "Specify a glob of tests to run. e.g '**/featureA/*.test.js' or 'tests/my-test.test.js'"
      )

    let browsers =
      Input.OptionWithMultipleStrings(
        [ "--browser"; "-b" ],
        [ "chromium"; "edge"; "chrome"; "webkit"; "firefox" ],
        "Which browsers to run the tests on, defaults to 'chromium'"
      )

    let headless =
      Input.Option(
        [ "--headless"; "-hl" ],
        "Turn on or off the Headless mode and open the browser (useful for debugging tests)"
      )

    let watch =
      Input.Option(
        [ "--watch"; "-w" ],
        "Start the server and keep watching for file changes"
      )

    let buildArgs
      (
        ctx: InvocationContext,
        files: string array option,
        browsers: string array,
        headless: bool option,
        watch: bool option
      ) =
      ctx.GetCancellationToken(),
      { files = files |> Option.defaultValue Array.empty
        browsers =
          if browsers.Length = 0 then [| "chromium" |] else browsers
          |> Array.map Browser.FromString
          |> set
        headless = headless |> Option.defaultValue true
        watch = watch |> Option.defaultValue false }

    command "test" {
      description "Runs client side tests in a headless browser"
      inputs (Input.Context(), files, browsers, headless, watch)
      setHandler (buildArgs >> Handlers.runTesting)
    }
