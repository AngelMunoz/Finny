namespace Perla

open System
open System.CommandLine
open System.CommandLine.Invocation
open System.CommandLine.Parsing
open System.IO

open System.Threading
open System.Threading.Tasks

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
open Perla.Extensibility

open Perla.Plugins
open Perla.PackageManager
open Perla.PackageManager.Types

open Perla.Testing
open Spectre.Console.Rendering


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
      enablePreview: bool
      enablePreloads: bool
      rebuildImportMap: bool }

  type SetupOptions =
    { skipPrompts: bool
      playwrightDeps: bool }

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
    { browsers: Browser seq option
      files: string seq option
      skip: string seq option
      watch: bool option
      headless: bool option
      browserMode: BrowserMode option }

  type DescribeOptions =
    { properties: string[] option
      current: bool }

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

      let username, repository, _ = chosen.Value

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
        "- Playwright browsers (requires admin privileges if using --with-playwright-deps)"

      Logger.log
        "After that you should be able to run 'perla build' or 'perla new' or 'perla test'"

      do! FileSystem.SetupEsbuild(UMX.tag Constants.Esbuild_Version)
      Logger.log ("[bold green]esbuild[/] has been setup!", escape = false)

      match options.skipPrompts, options.playwrightDeps with
      | true, true -> Testing.SetupPlaywright true
      | true, false -> Testing.SetupPlaywright false
      | false, true -> Testing.SetupPlaywright true
      | false, false ->
        if
          AnsiConsole.Confirm("Install Playwright with dependencies?", false)
        then
          Testing.SetupPlaywright true
        else
          Testing.SetupPlaywright false

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
      let config = ConfigurationManager.CurrentConfig

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
      | Some(username, repository, branch) ->
        match
          Templates.FindOne(TemplateSearchKind.FullName(username, repository))
        with
        | Some template ->
          let branch =
            if String.IsNullOrWhiteSpace branch then
              opts.branch
            else
              branch

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
      let config = ConfigurationManager.CurrentConfig

      let map = FileSystem.GetImportMap()

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

      ConfigurationManager.WriteFieldsToFile(
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
      ConfigurationManager.UpdateFromCliArgs(
        ?runConfig = options.mode,
        ?provider = options.source
      )

      let config = ConfigurationManager.CurrentConfig
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

      let importMap = FileSystem.GetImportMap()
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
        let newDep =
          { name = package
            version = packageVersion
            alias = options.alias }

        let dependencies, devDependencies =
          let config =
            match config.runConfiguration with
            | RunConfiguration.Development ->
              { config with
                  devDependencies = [ yield! config.devDependencies; newDep ] }
            | RunConfiguration.Production ->
              { config with
                  dependencies = [ yield! config.dependencies; newDep ] }

          let deps, devDeps =
            Dependencies.LocateDependenciesFromMapAndConfig(map, config)

          PerlaWritableField.Dependencies deps,
          PerlaWritableField.DevDependencies devDeps

        ConfigurationManager.WriteFieldsToFile(
          [ dependencies; devDependencies ]
        )

        FileSystem.WriteImportMap(map) |> ignore
        return 0
      | Error err ->
        Logger.log ($"[bold red]{err}[/]", escape = false)
        return 1
    }

  let runRestore (options: RestoreOptions) =
    task {
      ConfigurationManager.UpdateFromCliArgs(
        ?runConfig = options.mode,
        ?provider = options.source
      )

      let config = ConfigurationManager.CurrentConfig

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

  let maybeFable (config: PerlaConfig, cancel: CancellationToken) =
    task {
      match config.fable with
      | Some fable -> do! Fable.Start(fable, cancellationToken = cancel) :> Task
      | None ->
        Logger.log (
          "No Fable configuration provided, skipping fable",
          target = PrefixKind.Build
        )
    }

  let runBuild (cancel: CancellationToken, args: BuildOptions) =
    task {
      ConfigurationManager.UpdateFromCliArgs(?runConfig = args.mode)
      let config = ConfigurationManager.CurrentConfig

      do! maybeFable (config, cancel)

      if not <| File.Exists($"{FileSystem.EsbuildBinaryPath}") then
        do! FileSystem.SetupEsbuild(config.esbuild.version, cancel)

      let outDir = UMX.untag config.build.outDir

      try
        Directory.Delete(outDir, true)
        Directory.CreateDirectory(outDir) |> ignore
      with _ ->
        ()

      PluginRegistry.LoadPlugins(config.esbuild)

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
          let operation =

            if args.rebuildImportMap then
              let dependencies =
                match config.runConfiguration with
                | RunConfiguration.Production ->
                  config.dependencies |> Seq.map (fun d -> d.AsVersionedString)
                | RunConfiguration.Development ->
                  [ yield! config.dependencies; yield! config.devDependencies ]
                  |> Seq.map (fun d -> d.AsVersionedString)

              Dependencies.GetMapAndDependencies(dependencies, config.provider)
            else
              Dependencies.GetMapAndDependencies(
                FileSystem.GetImportMap(),
                config.provider
              )

          match!
            Logger.spinner (
              "Resolving Static dependencies and import map...",
              operation
            )
          with
          | Ok(deps, map) ->
            FileSystem.WriteImportMap(map) |> ignore
            let deps = if args.enablePreloads then deps else Seq.empty
            return Build.GetIndexFile(document, css, js, map, deps)
          | Error err ->
            Logger.log
              $"We were unable to update static dependencies and import map: {err}, falling back to the map in disk"

            let map = FileSystem.GetImportMap()
            return Build.GetIndexFile(document, css, js, map)
        }

      let outDir =
        config.build.outDir
        |> UMX.untag
        |> Path.GetFullPath
        |> fs.ConvertPathFromInternal


      // copy any glob files
      Build.CopyGlobs(config.build, tempDirectory)
      // copy any root files
      fs.EnumerateFileEntries(tmp, "*.*", SearchOption.TopDirectoryOnly)
      |> Seq.iter (fun file ->
        file.CopyTo(UPath.Combine(outDir, file.Name), true) |> ignore)

      // Always copy the index file at the end to avoid
      // clashing with any index.html file in the root of the virtual file system
      fs.WriteAllText(UPath.Combine(outDir, "index.html"), indexContent)


      Logger.log $"Cleaning up temp dir {tempDirectory}"

      try
        Directory.Delete(UMX.untag tempDirectory, true)
      with ex ->
        Logger.log ($"Failed to delete {tempDirectory}", ex = ex)

      if args.enablePreview then
        let app = Server.GetStaticServer(config)
        do! app.StartAsync(cancel)

        app.Urls
        |> Seq.iter (fun url ->
          Logger.log ($"Listening at: {url}", target = Logger.Serve))

        while not cancel.IsCancellationRequested do
          do! Async.Sleep(1000)

        do! app.StopAsync(cancel)

      return 0
    }

  let private firstCompileFinished
    isWatch
    (observable: IObservable<FableEvent>)
    =
    observable
    |> Observable.choose (function
      | FableEvent.WaitingForChanges -> Some()
      | _ -> None)
    |> (fun obs ->
      if isWatch then
        Observable.first obs
      else
        Observable.takeLast 1 obs)
    |> AsyncSeq.ofObservableBuffered
    |> AsyncSeq.iter ignore

  let private getFileChanges
    (
      index: string,
      mountDirectories,
      perlaFilesChanges,
      plugins: string list
    ) =
    let perlaFilesChanges =
      perlaFilesChanges
      |> Observable.map (fun event ->
        let name, path, extension =
          match event with
          | PerlaFileChange.Index ->
            (Path.GetFileName index, Path.GetFullPath index, ".html")
          | PerlaFileChange.PerlaConfig ->
            (Constants.PerlaConfigName,
             UMX.untag FileSystem.PerlaConfigPath,
             ".jsonc")
          | PerlaFileChange.ImportMap ->
            (Constants.ImportMapName,
             UMX.untag (FileSystem.GetConfigPath Constants.ImportMapName None),
             ".importmap")

        { serverPath = UMX.tag "/"
          userPath = UMX.tag "/"
          oldPath = None
          oldName = None
          changeType = ChangeKind.Changed
          path = UMX.tag path
          name = UMX.tag name },
        { content = ""; extension = extension })

    VirtualFileSystem.GetFileChangeStream mountDirectories
    |> VirtualFileSystem.ApplyVirtualOperations plugins
    |> Observable.merge perlaFilesChanges

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
          | Some ssl -> DevServerField.UseSSL ssl
          | None -> () ]

      ConfigurationManager.UpdateFromCliArgs(
        ?runConfig = options.mode,
        serverOptions = cliArgs
      )


      let config = ConfigurationManager.CurrentConfig

      let fableEvents =
        match config.fable with
        | Some fable -> Fable.Observe(fable, cancellationToken = cancel)
        | None ->
          let sub = Subject.replay
          sub.OnNext(FableEvent.WaitingForChanges)
          sub

      use _ =
        fableEvents
        |> Observable.subscribeSafe (fun events ->
          match events with
          | FableEvent.Log msg -> Logger.log (msg.EscapeMarkup())
          | FableEvent.ErrLog msg ->
            Logger.log $"[bold red]{msg.EscapeMarkup()}[/]"
          | FableEvent.WaitingForChanges -> ())

      do! firstCompileFinished true fableEvents

      do! FileSystem.SetupEsbuild(config.esbuild.version, cancel)

      PluginRegistry.LoadPlugins(config.esbuild)

      do! VirtualFileSystem.Mount(config)

      let perlaChanges =
        FileSystem.ObservePerlaFiles(UMX.untag config.index, cancel)

      let fileChanges =
        getFileChanges (
          UMX.untag config.index,
          config.mountDirectories,
          perlaChanges,
          config.plugins
        )

      // TODO: Grab these from esbuild
      let compilerErrors = Observable.empty

      let mutable app = Server.GetServerApp(config, fileChanges, compilerErrors)
      do! app.StartAsync(cancel)

      app.Urls
      |> Seq.iter (fun url ->
        Logger.log ($"Listening at: {url}", target = Logger.Serve))

      perlaChanges
      |> Observable.choose (function
        | PerlaFileChange.PerlaConfig -> Some()
        | _ -> None)
      |> Observable.map (fun _ -> app.StopAsync() |> Async.AwaitTask)
      |> Observable.switchAsync
      |> Observable.add (fun _ ->
        ConfigurationManager.UpdateFromFile()
        app <- Server.GetServerApp(config, fileChanges, compilerErrors)
        app.StartAsync(cancel) |> ignore)

      while not cancel.IsCancellationRequested do
        do! Async.Sleep(TimeSpan.FromSeconds(1.))

      return 0
    }

  /// set esbuild, playwright and import map dependencies in parallel
  /// as these are not overlaping and should save time
  let setupDependencies (config, cancel) =
    task {
      let! results =
        [ task {
            do Testing.SetupPlaywright false
            return None
          }
          task {
            do! FileSystem.SetupEsbuild(config.esbuild.version, cancel)
            return None
          }
          task {
            let! result =
              Logger.spinner (
                "Resolving Static dependencies and import map...",
                Dependencies.GetMapAndDependencies(
                  [ yield! config.dependencies; yield! config.devDependencies ]
                  |> Seq.map (fun d -> d.AsVersionedString),
                  config.provider
                )
              )

            return
              result
              |> Result.defaultWith (fun _ ->
                (Seq.empty, FileSystem.GetImportMap()))
              |> Some
          } ]
        |> Task.WhenAll

      return results[2].Value
    }

  let runTesting (cancel: CancellationToken, options: TestingOptions) =
    task {
      ConfigurationManager.UpdateFromCliArgs(
        testingOptions =
          [ match options.browsers with
            | Some value -> TestingField.Browsers value
            | None -> ()
            match options.files with
            | Some value -> TestingField.Includes value
            | None -> ()
            match options.skip with
            | Some value -> TestingField.Excludes value
            | None -> ()
            match options.watch with
            | Some value -> TestingField.Watch value
            | None -> ()
            match options.headless with
            | Some value -> TestingField.Headless value
            | None -> ()
            match options.browserMode with
            | Some value -> TestingField.BrowserMode value
            | None -> () ]
      )

      let config =
        { ConfigurationManager.CurrentConfig with
            mountDirectories =
              ConfigurationManager.CurrentConfig.mountDirectories
              |> Map.add
                (UMX.tag<ServerUrl> "/tests")
                (UMX.tag<UserPath> "./tests") }

      let isWatch = config.testing.watch

      let! dependencies = setupDependencies (config, cancel)

      let fableEvents =
        match config.testing.fable with
        | Some fable -> Fable.Observe(fable, isWatch)
        | None -> Observable.single FableEvent.WaitingForChanges

      fableEvents
      |> Observable.add (fun events ->
        match events with
        | FableEvent.Log msg -> Logger.log (msg.EscapeMarkup())
        | FableEvent.ErrLog msg ->
          Logger.log $"[bold red]{msg.EscapeMarkup()}[/]"
        | FableEvent.WaitingForChanges -> ())

      do! firstCompileFinished isWatch fableEvents


      PluginRegistry.LoadPlugins config.esbuild

      do! VirtualFileSystem.Mount config

      let perlaChanges =
        FileSystem.ObservePerlaFiles(UMX.untag config.index, cancel)

      let fileChanges =
        getFileChanges (
          UMX.untag config.index,
          config.mountDirectories,
          perlaChanges,
          config.plugins
        )
      // TODO: Grab these from esbuild
      let compilerErrors = Observable.empty

      let config =
        { config with
            devServer =
              { config.devServer with
                  liveReload = isWatch } }

      let events = Subject<TestEvent>.replay

      let mutable app =
        Server.GetTestingApp(
          config,
          dependencies,
          events,
          fileChanges,
          compilerErrors,
          config.testing.includes
        )
      // Keep this before initializing the server
      // otherwise it will always say that the port is occupied
      let http, _ =
        Server.GetServerURLs
          config.devServer.host
          config.devServer.port
          config.devServer.useSSL

      do! app.StartAsync(cancel)

      perlaChanges
      |> Observable.choose (function
        | PerlaFileChange.PerlaConfig -> Some()
        | _ -> None)
      |> Observable.map (fun _ -> app.StopAsync() |> Async.AwaitTask)
      |> Observable.switchAsync
      |> Observable.map (fun _ ->
        ConfigurationManager.UpdateFromFile()
        app <- Server.GetServerApp(config, fileChanges, compilerErrors)
        app.StartAsync(cancel) |> Async.AwaitTask)
      |> Observable.switchAsync
      |> Observable.add ignore


      use! pl = Playwright.CreateAsync()


      if not isWatch then
        let browsers =
          asyncSeq {
            for browser in config.testing.browsers do
              let! iBrowser =
                Testing.GetBrowser(pl, browser, config.testing.headless)
                |> Async.AwaitTask

              browser, iBrowser
          }

        let runTest (browser, iBrowser) =
          async {
            let executor = Testing.GetExecutor(http, browser)
            do! executor iBrowser |> Async.AwaitTask
            do! iBrowser.CloseAsync() |> Async.AwaitTask
            return! iBrowser.DisposeAsync().AsTask() |> Async.AwaitTask
          }

        match config.testing.browserMode with
        | BrowserMode.Parallel ->
          do!
            Async.StartAsTask(
              browsers |> AsyncSeq.iterAsyncParallel runTest,
              cancellationToken = cancel
            )
        | BrowserMode.Sequential ->
          do!
            Async.StartAsTask(
              browsers |> AsyncSeq.iterAsync runTest,
              cancellationToken = cancel
            )


        events.OnCompleted()

        events
        |> Observable.toEnumerable
        |> Seq.toList
        |> Testing.BuildReport
        |> Print.Report

        return 0
      else
        let browser = config.testing.browsers |> Seq.head

        let! iBrowser =
          Testing.GetBrowser(
            pl,
            config.testing.browsers |> Seq.head,
            config.testing.headless
          )

        let liveExecutor =
          Testing.GetLiveExecutor(
            http,
            browser,
            fileChanges |> Observable.map ignore
          )

        use _ = Testing.PrintReportLive events
        let! pageReloads = liveExecutor iBrowser

        use _ =
          pageReloads
          |> Observable.subscribeSafe (fun _ ->
            Logger.log $"Live Reload: Page Reloaded After Change")

        while not cancel.IsCancellationRequested do
          do! Async.Sleep(TimeSpan.FromSeconds(1.))

        events.OnCompleted()

        events
        |> Observable.toEnumerable
        |> Seq.toList
        |> Testing.BuildReport
        |> Print.Report

        return 0
    }

  let runDescribe (cancel: CancellationToken, options: DescribeOptions) =
    task {
      let { properties = props
            current = current } =
        options

      let config = ConfigurationManager.CurrentConfig

      let table = Table().AddColumn("Property")

      AnsiConsole.Write(FigletText("Perla.json"))
      let descriptions = FileSystem.DescriptionsFile

      match props, current with
      | Some props, true ->
        table.AddColumns("Value", "Explanation") |> ignore

        for prop in props do
          let description =
            descriptions.Value |> Map.tryFind prop |> Option.defaultValue ""

          match prop with
          | TopLevelProp prop ->
            table.AddRow(
              Text(prop),
              config[prop] |> Option.defaultValue (Text ""),
              Text(description)
            )
            |> ignore
          | NestedProp props ->
            table.AddRow(
              Text(prop),
              config[props] |> Option.defaultValue (Text ""),
              Text(description)
            )
            |> ignore
          | TripleNestedProp props ->
            table.AddRow(
              Text(prop),
              config[props] |> Option.defaultValue (Text ""),
              Text(description)
            )
            |> ignore
          | InvalidPropPath ->
            table.AddRow(prop, "", "This is not a valid property") |> ignore

      | Some props, false ->
        table.AddColumns("Description", "Default Value") |> ignore

        for prop in props do
          let description =
            descriptions.Value |> Map.tryFind prop |> Option.defaultValue ""

          match prop with
          | TopLevelProp prop ->
            table.AddRow(
              Text(prop),
              Text(description),
              Defaults.PerlaConfig[prop] |> Option.defaultValue (Text "")
            )
            |> ignore
          | NestedProp props ->
            table.AddRow(
              Text(prop),
              Text(description),
              Defaults.PerlaConfig[props] |> Option.defaultValue (Text "")
            )
            |> ignore
          | TripleNestedProp props ->
            table.AddRow(
              Text(prop),
              Text(description),
              Defaults.PerlaConfig[props] |> Option.defaultValue (Text "")
            )
            |> ignore
          | InvalidPropPath ->
            table.AddRow(
              Text(
                prop,
                Style(foreground = Color.Yellow, background = Color.Yellow)
              ),
              Text(""),
              Text(
                "This is not a valid property",
                Style(foreground = Color.Yellow)
              )
            )
            |> ignore

      | None, false ->
        table.AddColumn("Description") |> ignore

        for KeyValue(key, value) in descriptions.Value do
          table.AddRow(key, value) |> ignore
      | None, true ->
        table.AddColumns("Current Value", "Description") |> ignore

        for KeyValue(key, description) in descriptions.Value do
          match key with
          | TopLevelProp prop ->
            table.AddRow(
              Text(prop),
              Defaults.PerlaConfig[prop] |> Option.defaultValue (Text ""),
              Text(description)
            )
            |> ignore
          | NestedProp props ->
            table.AddRow(
              Text(key),
              Defaults.PerlaConfig[props] |> Option.defaultValue (Text ""),
              Text(description)
            )
            |> ignore
          | TripleNestedProp props ->
            table.AddRow(
              Text(key),
              Defaults.PerlaConfig[props] |> Option.defaultValue (Text ""),
              Text(description)
            )
            |> ignore
          | InvalidPropPath ->
            table.AddRow(
              Text(
                key,
                Style(foreground = Color.Yellow, background = Color.Yellow)
              ),
              Text(""),
              Text(
                "This is not a valid property",
                Style(foreground = Color.Yellow)
              )
            )
            |> ignore

      table.Caption <-
        TableTitle(
          "For more information visit: https://perla-docs.web.app/#/v1/docs/reference/perla"
        )

      table.DoubleBorder() |> AnsiConsole.Write
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

      static member MultipleStrings
        (
          name: string,
          ?values: string seq,
          ?description
        ) =
        let option =
          Option<string[] option>(
            name = name,
            ?description = description,
            IsRequired = false,
            AllowMultipleArgumentsPerToken = true,
            parseArgument =
              (fun result ->
                match result.Tokens |> Seq.toArray with
                | [||] -> None
                | others ->
                  Some(others |> Array.map (fun token -> token.Value)))
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
    let enablePreloads =
      Input.OptionMaybe(
        [ "-epl"; "--enable-preload-links" ],
        "enable adding modulepreload links in the final build"
      )

    let rebuildImportMap =
      Input.OptionMaybe(
        [ "-rim"; "--rebuild-importmap" ],
        "discards the current import map (and custom resolutions)
         and generates a new one based on the dependencies listed in the config file."
      )

    let preview =
      Input.OptionMaybe(
        [ "-prev"; "--preview" ],
        "discards the current import map (and custom resolutions)
         and generates a new one based on the dependencies listed in the config file."
      )

    let buildArgs
      (
        context: InvocationContext,
        runAsDev: bool option,
        enablePreloads: bool option,
        rebuildImportMap: bool option,
        enablePreview: bool option
      ) =
      (context.GetCancellationToken(),
       { mode =
           runAsDev
           |> Option.map (fun runAsDev ->
             match runAsDev with
             | true -> RunConfiguration.Development
             | false -> RunConfiguration.Production)
         enablePreloads = defaultArg enablePreloads true
         rebuildImportMap = defaultArg rebuildImportMap false
         enablePreview = defaultArg enablePreview false })

    command "build" {
      description "Builds the SPA application for distribution"

      inputs (
        Input.Context(),
        runAsDev,
        enablePreloads,
        rebuildImportMap,
        preview
      )

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

    let desc =
      "Starts the development server and if fable projects are present it also takes care of it."

    let serve =
      command "serve" {
        description desc
        inputs (Input.Context(), runAsDev, port, host, ssl)
        setHandler (buildArgs >> Handlers.runServe)
      }

    let serveShorthand =
      command "s" {
        description desc
        inputs (Input.Context(), runAsDev, port, host, ssl)
        setHandler (buildArgs >> Handlers.runServe)
      }

    serve, serveShorthand

  let Init =
    let skipPrompts =
      Input.OptionMaybe<bool>(
        [ "--skip-prompts"; "-y"; "--yes"; "-sp" ],
        "Skip prompts"
      )

    let playwrightDeps =
      Input.OptionMaybe<bool>(
        [ "--with-playwright-deps"; "-wpd"; "--pl-deps" ],
        "Install Playwright dependencies as well? (requires admin priviledges)"
      )

    let buildArgs
      (
        yes: bool option,
        playwrightDeps: bool option
      ) : SetupOptions =
      { skipPrompts = yes |> Option.defaultValue false
        playwrightDeps = playwrightDeps |> Option.defaultValue false }

    command "init" {
      description "Initialized a given directory or perla itself"
      inputs (skipPrompts, playwrightDeps)
      setHandler (buildArgs >> Handlers.runInit)
    }

  let SearchPackages =
    let package =
      Input.OptionRequired(
        "package",
        "The package you want to search for in the Skypack api"
      )

    let page =
      Input.OptionMaybe(
        [| "--page"; "-p" |],
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

    let browsers =
      Input.MultipleStrings(
        "--browser",
        [ "chromium"; "firefox"; "webkit"; "edge"; "chrome" ],
        "Which browsers to run the tests on, defaults to 'chromium'"
      )

    let files: HandlerInput<string[]> =
      Input.Option<string[]>(
        [ "--tests"; "-t" ],
        [||],
        "Specify a glob of tests to run. e.g '**/featureA/*.test.js' or 'tests/my-test.test.js'"
      )

    let skips: HandlerInput<string[]> =
      Input.Option<string[]>(
        [ "--skip"; "-s" ],
        [||],
        "Specify a glob of tests to skip. e.g '**/featureA/*.test.js' or 'tests/my-test.test.js'"
      )


    let headless: HandlerInput<bool option> =
      Input.OptionMaybe<bool>(
        [ "--headless"; "-hl" ],
        "Turn on or off the Headless mode and open the browser (useful for debugging tests)"
      )

    let watch: HandlerInput<bool option> =
      Input.OptionMaybe<bool>(
        [ "--watch"; "-w" ],
        "Start the server and keep watching for file changes"
      )

    let sequential: HandlerInput<bool option> =
      Input.OptionMaybe<bool>(
        [ "--browser-sequential"; "-bs" ],
        "Run each browser's test suite in sequence, rather than parallel"
      )

    let buildArgs
      (
        ctx: InvocationContext,
        browsers: string array option,
        files: string array,
        skips: string array,
        headless: bool option,
        watch: bool option,
        sequential: bool option
      ) : CancellationToken * TestingOptions =
      ctx.GetCancellationToken(),
      { browsers =
          browsers
          |> Option.map (fun browsers ->
            if browsers |> Array.isEmpty then
              None
            else
              Some(browsers |> Seq.map Browser.FromString))
          |> Option.flatten
        files = if files |> Array.isEmpty then None else Some files
        skip = if skips |> Array.isEmpty then None else Some skips
        headless = headless
        watch = watch
        browserMode =
          sequential
          |> Option.map (fun sequential ->
            if sequential then Some BrowserMode.Sequential else None)
          |> Option.flatten }

    command "test" {
      description "Runs client side tests in a headless browser"

      inputs (
        Input.Context(),
        browsers,
        files,
        skips,
        headless,
        watch,
        sequential
      )

      setHandler (buildArgs >> Handlers.runTesting)
    }

  let Describe =
    let perlaProperties: HandlerInput<string[] option> =
      Arg<string[] option>(
        "properties",
        (fun (result: ArgumentResult) ->
          match result.Tokens |> Seq.toArray with
          | [||] -> None
          | others -> Some(others |> Array.map (fun token -> token.Value))),
        Description =
          "A property, properties or json path-like string names to describe",
        Arity = ArgumentArity.ZeroOrMore
      )
      |> HandlerInput.OfArgument

    let describeCurrent: HandlerInput<bool> =
      Input.Option(
        [ "--current"; "-c" ],
        false,
        "Take my current perla.json file and print my current configuration"
      )

    let buildArgs
      (
        ctx: InvocationContext,
        properties: string[] option,
        current: bool
      ) =
      ctx.GetCancellationToken(),
      { properties = properties
        current = current }

    command "describe" {
      description
        "Describes the perla.json file or it's properties as requested"

      inputs (Input.Context(), perlaProperties, describeCurrent)
      setHandler (buildArgs >> Handlers.runDescribe)
    }
