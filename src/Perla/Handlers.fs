namespace Perla.Handlers

open System
open System.IO

open System.Threading
open System.Threading.Tasks

open AngleSharp.Html.Parser
open Microsoft.Playwright
open Spectre.Console

open FSharp.Control
open FSharp.Control.Reactive

open FSharp.UMX
open FsToolkit.ErrorHandling

open AngleSharp
open Zio.FileSystems
open Zio

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

[<Struct; RequireQualifiedAccess>]
type ListFormat =
  | HumanReadable
  | TextOnly

type ServeOptions = {
  port: int option
  host: string option
  mode: RunConfiguration option
  ssl: bool option
}

type BuildOptions = {
  mode: RunConfiguration option
  enablePreview: bool
  enablePreloads: bool
  rebuildImportMap: bool
}

type SetupOptions = {
  installTemplates: bool
  skipPrompts: bool
}

type SearchOptions = { package: string; page: int }

type ShowPackageOptions = { package: string }

type ListTemplatesOptions = { format: ListFormat }

type AddPackageOptions = {
  package: string
  version: string option
  source: Provider option
  mode: RunConfiguration option
  alias: string option
}

type RemovePackageOptions = {
  package: string
  alias: string option
}

type ListPackagesOptions = { format: ListFormat }

[<RequireQualifiedAccess; Struct>]
type RunTemplateOperation =
  | Add
  | Update
  | Remove
  | List of ListFormat

type TemplateRepositoryOptions = {
  fullRepositoryName: string
  operation: RunTemplateOperation
}

type ProjectOptions = {
  projectName: string
  byTemplateName: string option
  byId: string option
  byShortName: string option
}

type RestoreOptions = {
  source: Provider option
  mode: RunConfiguration option
}

type TestingOptions = {
  browsers: Browser seq option
  files: string seq option
  skip: string seq option
  watch: bool option
  headless: bool option
  browserMode: BrowserMode option
}

type DescribeOptions = {
  properties: string[] option
  current: bool
}

module Templates =

  type FoundTemplate =
    | Repository of PerlaTemplateRepository
    | Existing of TemplateItem

  type TemplateNotFoundCases =
    | NoQueryParams
    | ParentTemplateNotFound
    | ChildTemplateNotFound

  [<RequireQualifiedAccess; Struct>]
  type TemplateOperation =
    | Add of repoName: (string * string * string)
    | Update of foundRepo: PerlaTemplateRepository

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


  let List (options: ListTemplatesOptions) =
    let results =
      Templates.ListTemplateItems()
      |> List.groupBy (fun x -> x.parent)
      |> List.choose (fun (parentId, children) ->
        match Templates.FindOne(TemplateSearchKind.Id parentId) with
        | Some parent -> Some(parent, children)
        | None -> None)
      |> List.collect (fun (parent, children) ->
        children |> List.map (fun child -> (parent, child)))

    match options.format with
    | ListFormat.HumanReadable ->
      let table =
        Table()
          .AddColumns(
            [|
              "Name"
              "Group"
              "Belongs to"
              "Parent Location"
              "Last Update"
            |]
          )

      for column in table.Columns do
        column.Alignment <- Justify.Center

      for parent, template in results do
        let name: Rendering.IRenderable =
          Markup($"[bold green]{template.name}[/]")

        let group = Markup($"[bold yellow]{template.group}[/]")
        let belongsTo = Markup($"[bold blue]{parent.ToFullNameWithBranch}[/]")

        let lastUpdate =
          parent.updatedAt
          |> Option.ofNullable
          |> Option.defaultValue parent.createdAt
          |> (fun x -> $"[bold green]{x.ToShortDateString()}[/]")
          |> Markup

        let location =
          TextPath(
            UMX.untag parent.path,
            LeafStyle = Style(Color.Green),
            StemStyle = Style(Color.Yellow),
            SeparatorStyle = Style(Color.Blue)
          )

        table.AddRow([| name; group; belongsTo; location; lastUpdate |])
        |> ignore

      AnsiConsole.Write table
      0
    | ListFormat.TextOnly ->
      let columns =
        Columns([| "Name"; "Group"; "Belongs to"; "Parent Location" |])

      AnsiConsole.Write columns

      let rows = [|
        for parent, template in results do
          let name = Markup($"[bold green]{template.name}[/]")
          let group = Markup($"[bold yellow]{template.group}[/]")

          let belongsTo = Markup($"[bold blue]{parent.ToFullNameWithBranch}[/]")

          let location =
            TextPath(
              UMX.untag parent.path,
              LeafStyle = Style(Color.Green),
              StemStyle = Style(Color.Yellow),
              SeparatorStyle = Style(Color.Blue)
            )

          Columns(name, group, belongsTo, location) :> Rendering.IRenderable
      |]

      rows |> Rows |> AnsiConsole.Write

      0

  let AddOrUpdate (operation: TemplateOperation) = taskResult {
    let listTemplates () =
      List { format = ListFormat.HumanReadable } |> ignore

      Logger.log (
        "[bold yellow]perla[/] [bold blue]new [/] [bold blue] <PROJECT_NAME>[/]",
        escape = false
      )

      Logger.log "Feel free to create a new perla project"

    match operation with
    | TemplateOperation.Add repoName ->
      let username, repository, branch = repoName

      do!
        Logger.spinner (
          $"Adding templates from: {username}/{repository}:{branch}",
          (addTemplate username repository branch)
        )
        |> TaskResult.ignore

      listTemplates ()
      return ()
    | TemplateOperation.Update template ->
      do! taskResult {
        let! result =
          Logger.spinner (
            $"Updating templates from: {template.ToFullNameWithBranch}",
            (updateTemplate template template.branch)
          )

        if result then
          return ()
        else
          return!
            Error
              "We were unable to update the existing [bold red]templates[/]."
      }

      listTemplates ()
      return ()
  }

  let Remove (template: PerlaTemplateRepository) : Result<unit, string> =
    Templates.Delete(TemplateSearchKind.Id template._id)
    |> Result.requireTrue
      "There was an error while trying to delete this template."

module Fable =
  let StartFable (config: PerlaConfig, cancel: CancellationToken) = task {
    match config.fable with
    | Some fable -> do! Fable.Start(fable, cancellationToken = cancel) :> Task
    | None ->
      Logger.log (
        "No Fable configuration provided, skipping fable",
        target = PrefixKind.Build
      )
  }

module FsMonitor =
  let FirstCompileDone isWatch (observable: IObservable<FableEvent>) =
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

  let FileChanges
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
             ".json")
          | PerlaFileChange.ImportMap ->
            (Constants.ImportMapName,
             UMX.untag (FileSystem.GetConfigPath Constants.ImportMapName None),
             ".importmap")

        {
          serverPath = UMX.tag "/"
          userPath = UMX.tag "/"
          oldPath = None
          oldName = None
          changeType = ChangeKind.Changed
          path = UMX.tag path
          name = UMX.tag name
        },
        { content = ""; extension = extension })

    VirtualFileSystem.GetFileChangeStream mountDirectories
    |> VirtualFileSystem.ApplyVirtualOperations plugins
    |> Observable.merge perlaFilesChanges

module Esbuild =

  let Run
    (
      config: PerlaConfig,
      workingDirectory: UPath,
      fs: IFileSystem,
      (css, js): string<ServerUrl> seq * string<ServerUrl> seq,
      externals: string seq,
      cancel: CancellationToken
    ) =
    let cssTasks = backgroundTask {
      for css in css do
        let path =
          UPath.Combine(workingDirectory, UMX.untag css)
          |> fs.ConvertPathToInternal

        let targetPath =
          Path.Combine(UMX.untag config.build.outDir, UMX.untag css)
          |> Path.GetDirectoryName
          |> UMX.tag<SystemPath>

        let tsk =
          Esbuild
            .ProcessCss(path, config.esbuild, targetPath)
            .ExecuteAsync(cancel)

        do! tsk.Task :> Task
    }

    let jsTasks = backgroundTask {
      for js in js do
        let path =
          UPath.Combine(workingDirectory, UMX.untag js)
          |> fs.ConvertPathToInternal

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

    Task.WhenAll(cssTasks, jsTasks)

module Setup =
  /// set esbuild, playwright and import map dependencies in parallel
  /// as these are not overlapping and should save time
  let EnsureDependencies (config, cancel) = task {
    let! results =
      [
        task {
          do Testing.SetupPlaywright()
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
        }
      ]
      |> Task.WhenAll

    return results[2].Value
  }

module Testing =
  let RunOnce
    (
      pl: IPlaywright,
      browserMode: BrowserMode,
      browsers: Browser seq,
      isHeadless: bool,
      url: string
    ) =
    let browsers = asyncSeq {
      for browser in browsers do
        let! iBrowser =
          Testing.GetBrowser(pl, browser, isHeadless) |> Async.AwaitTask

        browser, iBrowser
    }

    let runTest (browser, iBrowser) = async {
      let executor = Testing.GetExecutor(url, browser)
      do! executor iBrowser |> Async.AwaitTask
      do! iBrowser.CloseAsync() |> Async.AwaitTask
      return! iBrowser.DisposeAsync().AsTask() |> Async.AwaitTask
    }

    match browserMode with
    | BrowserMode.Parallel ->
      browsers |> AsyncSeq.iterAsyncParallelThrottled 2 runTest
    | BrowserMode.Sequential -> browsers |> AsyncSeq.iterAsync runTest

  let LiveRun
    (
      pl: IPlaywright,
      browser: Browser,
      isHeadless: bool,
      url: string,
      fileChanges: IObservable<unit>,
      broadcast: IObservable<TestEvent>,
      cancel: CancellationToken
    ) =
    task {

      let! iBrowser = Testing.GetBrowser(pl, browser, isHeadless)

      let liveExecutor =
        Testing.GetLiveExecutor(
          url,
          browser,
          fileChanges |> Observable.map ignore
        )

      use _ = Testing.PrintReportLive broadcast
      let! pageReloads = liveExecutor iBrowser

      use _ =
        pageReloads
        |> Observable.subscribeSafe (fun _ ->
          Logger.log $"Live Reload: Page Reloaded After Change")

      while not cancel.IsCancellationRequested do
        do! Async.Sleep(TimeSpan.FromSeconds(1.))
    }

[<RequireQualifiedAccess>]
module Handlers =
  open Perla.Database

  let runSetup (options: SetupOptions, cancellationToken: CancellationToken) = task {
    Logger.log "Perla will set up the following resources:"
    Logger.log "- Esbuild"
    Logger.log "- Default Templates"

    Logger.log
      "After that you should be able to run perla commands without extra effort."

    do!
      FileSystem.SetupEsbuild(
        UMX.tag Constants.Esbuild_Version,
        cancellationToken
      )

    Checks.SaveEsbuildBinPresent(UMX.tag Constants.Esbuild_Version) |> ignore

    Logger.log ("[bold green]esbuild[/] has been setup!", escape = false)

    let username, repository, branch =
      PerlaTemplateRepository.DefaultTemplatesRepository

    let getTemplate () =
      TemplateSearchKind.FullName(username, repository)
      |> Templates.FindOne
      |> Option.map (fun template ->
        Templates.AddOrUpdate(Templates.TemplateOperation.Update template))
      |> Option.defaultWith (fun () ->
        Templates.AddOrUpdate(
          Templates.TemplateOperation.Add(username, repository, branch)
        ))

    match options.skipPrompts, options.installTemplates with
    | false, true
    | true, true ->
      let! operation = getTemplate ()

      match operation with
      | Ok() ->
        Checks.SaveTemplatesPresent() |> ignore
        return 0
      | Error err ->
        Logger.log err
        return 1
    | true, false ->
      if AnsiConsole.Confirm("Add default templates?", false) then
        let! operation = getTemplate ()

        match operation with
        | Ok() ->
          Checks.SaveTemplatesPresent() |> ignore
          return 0
        | Error err ->
          Logger.log err
          return 1
      else
        Logger.log "Skip installing templates"
        return 0
    | false, false ->
      Logger.log "Skip installing templates"
      return 0


  }

  let runNew
    (
      options: ProjectOptions,
      cancellationToken: CancellationToken
    ) : Task<int> =
    Logger.log "Creating new project..."
    let mutable mentionQuickCommand = false

    let inline byId () =
      options.byId
      |> Option.map UMX.tag<TemplateGroup>
      |> Option.map QuickAccessSearch.Group

    let inline byTemplateName () =
      options.byTemplateName |> Option.map QuickAccessSearch.Name

    let queryParam =
      options.byShortName
      |> Option.map QuickAccessSearch.ShortName
      |> Option.orElseWith byId
      |> Option.orElseWith byTemplateName

    let foundRepo = result {
      let! query = queryParam |> Result.requireSome Templates.NoQueryParams

      match query with
      | QuickAccessSearch.Name name ->
        let user, template, child = getTemplateAndChild name

        let! found =
          (match user, child with
           | Some user, _ ->
             Templates.FindOne(TemplateSearchKind.FullName(user, template))
           | None, _ ->
             Templates.FindOne(TemplateSearchKind.Repository template))
          |> Result.requireSome Templates.ParentTemplateNotFound

        return Templates.Repository found
      | others ->
        let! found =
          Templates.FindTemplateItems(others)
          |> Result.requireHead Templates.ChildTemplateNotFound

        return Templates.Existing found
    }

    let inline TemplateItemPromptConverter (item: TemplateItem) =
      let description =
        item.description |> Option.defaultValue "No description provided."

      $"{item.name} - {item.shortName}: {description[0..30]}"

    let inline TemplateConfigPromptConverter (item: TemplateConfigurationItem) =
      $"{item.name} - {item.shortName}: {item.description[0..30]}"

    let selectedItem =
      match foundRepo with
      | Ok(Templates.Repository repo) ->
        mentionQuickCommand <- true

        let selection =
          SelectionPrompt(
            Title = $"Available templates for {repo.name}",
            Converter = TemplateConfigPromptConverter
          )
            .AddChoices(repo.templates)

        task {
          let! result =
            selection.ShowAsync(AnsiConsole.Console, cancellationToken)

          return
            Templates.FindTemplateItems(QuickAccessSearch.Id result.childId)
            |> List.tryHead
        }
      | Ok(Templates.Existing item) -> Task.FromResult(Some item)
      | Error Templates.NoQueryParams ->
        mentionQuickCommand <- true

        let selection =
          SelectionPrompt(
            Title = "Welcome to Perla, please select a template to start with",
            Converter = TemplateItemPromptConverter
          )
            .AddChoices(Templates.ListTemplateItems())

        task {
          try
            let! result =
              selection.ShowAsync(AnsiConsole.Console, cancellationToken)

            return Some result
          with :? TaskCanceledException ->
            return None
        }
      | Error Templates.ChildTemplateNotFound
      | Error Templates.ParentTemplateNotFound ->
        if
          AnsiConsole.Ask(
            "We were not able to find the template you were looking for, Would you like to check the existing templates?",
            false
          )
        then
          mentionQuickCommand <- true

          let selection =
            SelectionPrompt(
              Title = "Please select a template to start with",
              Converter = TemplateItemPromptConverter
            )
              .AddChoices(Templates.ListTemplateItems())

          task {
            try
              let! result =
                selection.ShowAsync(AnsiConsole.Console, cancellationToken)

              return Some result
            with :? TaskCanceledException ->
              return None
          }
        else
          Task.FromResult None

    task {
      let! item = selectedItem

      match item with
      | Some item ->
        let scriptContent =
          Templates.GetTemplateScriptContent(TemplateScriptKind.Template item)
          |> Option.orElseWith (fun () -> option {
            let! repo = Templates.FindOne(TemplateSearchKind.Id item.parent)

            return!
              TemplateScriptKind.Repository repo
              |> Templates.GetTemplateScriptContent
          })

        let targetPath =
          $"./{options.projectName}" |> Path.GetFullPath |> UMX.tag<UserPath>

        FileSystem.WriteTplRepositoryToDisk(
          item.fullPath,
          targetPath,
          ?payload = scriptContent
        )


        if mentionQuickCommand then
          let ffCmd =
            $"perla new [blue]<my-project-name>[/] [yellow]-t {item.shortName}[/]"

          let groupCmd =
            $"perla new [blue]<my-project-name>[/] [yellow]-id {item.group}[/]"

          Logger.log (
            $"You can run this template directly with:\n{ffCmd}\n{groupCmd}",
            escape = false
          )

        let chdir = $"cd ./{options.projectName}"
        let serve = "perla serve"

        Logger.log (
          $"Project [green]{options.projectName}[/] created!, to get started run:\n{chdir}\n{serve}",
          escape = false
        )

        return 0

      | None ->
        Logger.log "No selection was available..."

        Logger.log
          "please check for typos or run 'perla new <my-project-name>' to run the templating wizard"

        return 1
    }

  let runTemplate
    (
      options: TemplateRepositoryOptions,
      cancellationToken: CancellationToken
    ) =
    task {
      let template = voption {
        let! username, repository, _ =
          parseFullRepositoryName options.fullRepositoryName

        return!
          TemplateSearchKind.FullName(username, repository) |> Templates.FindOne
      }

      let updateRepo () = task {
        let template = template.Value
        Logger.log $"Template {template.ToFullNameWithBranch} already exists."

        match!
          Templates.AddOrUpdate(Templates.TemplateOperation.Update template)
        with
        | Ok() -> return 0
        | Error err ->
          Logger.log (err, escape = false)
          return 1
      }

      match options.operation with
      | RunTemplateOperation.List listFormat ->
        return Templates.List { format = listFormat }
      | RunTemplateOperation.Add ->
        match parseFullRepositoryName options.fullRepositoryName with
        | ValueSome template ->
          match!
            Templates.AddOrUpdate(Templates.TemplateOperation.Add template)
          with
          | Ok() -> return 0
          | Error err ->
            Logger.log (err, escape = false)
            return 1
        | ValueNone ->
          Logger.log ("We were unable to parse the repository name.")

          Logger.log (
            "please ensure that the repository name is in the format: [bold blue]username/repository:branch[/]",
            escape = false
          )

          return 1
      | RunTemplateOperation.Update -> return! updateRepo ()
      | RunTemplateOperation.Update
      | RunTemplateOperation.Add when template.IsSome -> return! updateRepo ()
      | RunTemplateOperation.Remove ->
        match template with
        | ValueSome template ->
          Logger.log $"Removing template '{template.ToFullNameWithBranch}'..."

          match Templates.Remove(template) with
          | Ok() ->
            Logger.log "Template removed successfully."
            return 0
          | Error err ->
            Logger.log (err, escape = false)
            return 1
        | ValueNone ->
          Logger.log ("We were unable to parse the repository name.")

          Logger.log (
            "please ensure that the repository name is in the format: [bold blue]username/repository:branch[/]",
            escape = false
          )

          return 1
    }


  let runBuild (options: BuildOptions, cancellationToken: CancellationToken) = task {
    FileSystem.GetDotEnvFilePaths() |> Env.LoadEnvFiles

    ConfigurationManager.UpdateFromCliArgs(?runConfig = options.mode)
    let config = ConfigurationManager.CurrentConfig

    do! Fable.StartFable(config, cancellationToken)

    if not <| File.Exists($"{FileSystem.EsbuildBinaryPath}") then
      do! FileSystem.SetupEsbuild(config.esbuild.version, cancellationToken)

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

    use fs = new PhysicalFileSystem()

    use browserCtx = new BrowsingContext()

    let externals = Build.GetExternals(config)

    let index = FileSystem.IndexFile(config.index)

    let document = browserCtx.GetService<IHtmlParser>().ParseDocument index

    let tmp = UMX.untag tempDirectory |> fs.ConvertPathFromInternal

    let css, js = Build.GetEntryPoints(document)

    do!
      Logger.spinner (
        "Transpiling CSS and JS Files",
        Esbuild.Run(config, tmp, fs, (css, js), externals, cancellationToken)
      )
      :> Task

    let css = [
      yield! css
      yield!
        js
        |> Seq.map (fun p ->
          Path.ChangeExtension(UMX.untag p, ".css") |> UMX.tag)
    ]


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

    let indexContent =
      Build.GetIndexFile(document, css, js, FileSystem.GetImportMap())
    // Always copy the index file at the end to avoid
    // clashing with any index.html file in the root of the virtual file system
    fs.WriteAllText(UPath.Combine(outDir, "index.html"), indexContent)

    Logger.log $"Cleaning up temp dir {tempDirectory}"

    try
      Directory.Delete(UMX.untag tempDirectory, true)
    with ex ->
      Logger.log ($"Failed to delete {tempDirectory}", ex = ex)

    if config.build.emitEnvFile then
      Logger.log "Writing Env File"
      Build.EmitEnvFile(config)

    if options.enablePreview then
      let app = Server.GetStaticServer(config)
      do! app.StartAsync(cancellationToken)

      app.Urls
      |> Seq.iter (fun url ->
        Logger.log ($"Listening at: {url}", target = Serve))

      while not cancellationToken.IsCancellationRequested do
        do! Async.Sleep(1000)

      do! app.StopAsync(cancellationToken)

    return 0
  }

  let runServe (options: ServeOptions, cancellationToken: CancellationToken) = task {
    FileSystem.GetDotEnvFilePaths() |> Env.LoadEnvFiles

    let cliArgs = [
      match options.port with
      | Some port -> DevServerField.Port port
      | None -> ()
      match options.host with
      | Some host -> DevServerField.Host host
      | None -> ()
      match options.ssl with
      | Some ssl -> DevServerField.UseSSL ssl
      | None -> ()
      // Don't minify sources in dev mode
      DevServerField.MinifySources false
    ]

    ConfigurationManager.UpdateFromCliArgs(
      ?runConfig = options.mode,
      serverOptions = cliArgs
    )


    let config = ConfigurationManager.CurrentConfig

    let fableEvents =
      match config.fable with
      | Some fable ->
        Fable.Observe(fable, cancellationToken = cancellationToken)
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

    do! FsMonitor.FirstCompileDone true fableEvents

    do! FileSystem.SetupEsbuild(config.esbuild.version, cancellationToken)

    PluginRegistry.LoadPlugins(config.esbuild)

    do! VirtualFileSystem.Mount(config)

    let perlaChanges =
      FileSystem.ObservePerlaFiles(UMX.untag config.index, cancellationToken)

    let fileChanges =
      FsMonitor.FileChanges(
        UMX.untag config.index,
        config.mountDirectories,
        perlaChanges,
        config.plugins
      )

    // TODO: Grab these from esbuild
    let compilerErrors = Observable.empty

    let mutable app = Server.GetServerApp(config, fileChanges, compilerErrors)
    do! app.StartAsync(cancellationToken)

    app.Urls
    |> Seq.iter (fun url -> Logger.log ($"Listening at: {url}", target = Serve))

    perlaChanges
    |> Observable.throttle (TimeSpan.FromMilliseconds(500.))
    |> Observable.choose (function
      | PerlaFileChange.PerlaConfig -> Some()
      | _ -> None)
    |> Observable.map (fun _ -> app.StopAsync() |> Async.AwaitTask)
    |> Observable.switchAsync
    |> Observable.add (fun _ ->
      ConfigurationManager.UpdateFromFile()
      app <- Server.GetServerApp(config, fileChanges, compilerErrors)
      app.StartAsync(cancellationToken) |> ignore)

    while not cancellationToken.IsCancellationRequested do
      do! Async.Sleep(TimeSpan.FromSeconds(1.))

    return 0
  }

  let runTesting
    (
      options: TestingOptions,
      cancellationToken: CancellationToken
    ) =
    task {
      FileSystem.GetDotEnvFilePaths() |> Env.LoadEnvFiles

      ConfigurationManager.UpdateFromCliArgs(
        testingOptions = [
          match options.browsers with
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
          | None -> ()
        ]
      )

      let config = {
        ConfigurationManager.CurrentConfig with
            mountDirectories =
              ConfigurationManager.CurrentConfig.mountDirectories
              |> Map.add
                (UMX.tag<ServerUrl> "/tests")
                (UMX.tag<UserPath> "./tests")
      }

      let isWatch = config.testing.watch

      let! dependencies = Setup.EnsureDependencies(config, cancellationToken)

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

      do! FsMonitor.FirstCompileDone isWatch fableEvents


      PluginRegistry.LoadPlugins config.esbuild

      do! VirtualFileSystem.Mount config

      let perlaChanges =
        FileSystem.ObservePerlaFiles(UMX.untag config.index, cancellationToken)

      let fileChanges =
        FsMonitor.FileChanges(
          UMX.untag config.index,
          config.mountDirectories,
          perlaChanges,
          config.plugins
        )
      // TODO: Grab these from esbuild
      let compilerErrors = Observable.empty

      let config = {
        config with
            devServer = {
              config.devServer with
                  liveReload = isWatch
            }
      }

      let events = Subject<TestEvent>.broadcast

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

      do! app.StartAsync(cancellationToken)

      perlaChanges
      |> Observable.choose (function
        | PerlaFileChange.PerlaConfig -> Some()
        | _ -> None)
      |> Observable.map (fun _ -> app.StopAsync() |> Async.AwaitTask)
      |> Observable.switchAsync
      |> Observable.map (fun _ ->
        ConfigurationManager.UpdateFromFile()
        app <- Server.GetServerApp(config, fileChanges, compilerErrors)
        app.StartAsync(cancellationToken) |> Async.AwaitTask)
      |> Observable.switchAsync
      |> Observable.add ignore

      use! pl = Playwright.CreateAsync()

      let testConfig = config.testing

      if not isWatch then
        do!
          Testing.RunOnce(
            pl,
            testConfig.browserMode,
            testConfig.browsers,
            testConfig.headless,
            http
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
        let fileChanges = fileChanges |> Observable.map ignore

        do!
          Testing.LiveRun(
            pl,
            browser,
            testConfig.headless,
            http,
            fileChanges,
            events,
            cancellationToken
          )

        events.OnCompleted()

        events
        |> Observable.toEnumerable
        |> Seq.toList
        |> Testing.BuildReport
        |> Print.Report

        return 0
    }

  let runSearchPackage
    (
      options: SearchOptions,
      cancellationToken: CancellationToken
    ) =
    task {
      do! Dependencies.Search(options.package, options.page)
      return 0
    }

  let runShowPackage
    (
      options: ShowPackageOptions,
      cancellationToken: CancellationToken
    ) =
    task {
      do! Dependencies.Show(options.package)
      return 0
    }

  let runAddPackage
    (
      options: AddPackageOptions,
      cancellationToken: CancellationToken
    ) =
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
        let newDep = {
          name = package
          version = packageVersion
          alias = options.alias
        }

        let dependencies, devDependencies =
          let config =
            match config.runConfiguration with
            | RunConfiguration.Development -> {
                config with
                    devDependencies = [ yield! config.devDependencies; newDep ]
              }
            | RunConfiguration.Production -> {
                config with
                    dependencies = [ yield! config.dependencies; newDep ]
              }

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

  let runRemovePackage
    (
      options: RemovePackageOptions,
      cancellationToken: CancellationToken
    ) =
    task {
      let name = options.package
      Logger.log ($"Removing: [red]{name}[/]", escape = false)
      let config = ConfigurationManager.CurrentConfig

      let map = FileSystem.GetImportMap()

      let dependencies, devDependencies =
        let deps, devDeps =
          Dependencies.LocateDependenciesFromMapAndConfig(
            map,
            {
              config with
                  dependencies =
                    config.dependencies |> Seq.filter (fun d -> d.name <> name)
                  devDependencies =
                    config.devDependencies
                    |> Seq.filter (fun d -> d.name <> name)
            }
          )

        deps, devDeps

      ConfigurationManager.WriteFieldsToFile(
        [
          PerlaWritableField.Dependencies dependencies
          PerlaWritableField.DevDependencies devDependencies
        ]
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

  let runListPackages (options: ListPackagesOptions) = task {
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

      let depsMap = config.dependencies |> Seq.map aliasDependency |> Map.ofSeq

      let devDepsMap =
        config.devDependencies |> Seq.map aliasDependency |> Map.ofSeq

      {|
        dependencies = depsMap
        devDependencies = devDepsMap
      |}
      |> Json.ToText
      |> AnsiConsole.Write

    return 0
  }

  let runRestoreImportMap
    (
      options: RestoreOptions,
      cancellationToken: CancellationToken
    ) =
    task {
      ConfigurationManager.UpdateFromCliArgs(
        ?runConfig = options.mode,
        ?provider = options.source
      )

      let config = ConfigurationManager.CurrentConfig

      Logger.log "Regenerating import map..."

      let packages =
        [
          yield! config.dependencies
          match config.runConfiguration with
          | RunConfiguration.Development -> yield! config.devDependencies
          | RunConfiguration.Production -> ()
        ]
        |> List.map (fun d -> d.AsVersionedString)
        // deduplicate repeated strings
        |> set

      match!
        Logger.spinner (
          "Fetching dependencies...",
          Dependencies.Restore(
            packages,
            provider = config.provider,
            runConfig = config.runConfiguration
          )
        )
      with
      | Ok response ->
        FileSystem.WriteImportMap(response) |> ignore
        return 0
      | Error err ->
        Logger.log (
          $"[bold red]An error happened restoring the import map:[/]",
          escape = false
        )

        Logger.log err
        return 1
    }

  let runDescribePerla (options: DescribeOptions) = task {
    let {
          properties = props
          current = current
        } =
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
