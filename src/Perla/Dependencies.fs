namespace Perla

open System
open System.Runtime.InteropServices
open Spectre.Console

open Perla.Logger
open Perla.PackageManager
open Perla.PackageManager.Types
open Perla.PackageManager.Skypack

open FsToolkit.ErrorHandling
open Types


module Dependencies =

  let printSearchTable (searchData: PackageSearchResult seq) =
    let table =
      Table()
        .AddColumn(TableColumn("Name"))
        .AddColumn(TableColumn("Description"))
        .AddColumn(TableColumn("Maintainers"))
        .AddColumn(TableColumn("Last Updated"))

    for row in searchData do
      let maintainers =
        row.maintainers
        |> Seq.truncate 3
        |> Seq.map (fun maintainer ->
          $"[yellow]{maintainer.name}[/] - [yellow]{maintainer.email}[/]")

      let maintainers = String.Join("\n", maintainers)

      table.AddRow(
        Markup($"[bold green]{row.name}[/]"),
        Markup(row.description),
        Markup($"{maintainers}..."),
        Markup(row.updatedAt.ToShortDateString())
      )
      |> ignore

    AnsiConsole.Write table

  let printShowTable (package: PackageInfo) =
    let table =
      Table()
        .AddColumn(TableColumn("Description"))
        .AddColumn(TableColumn("Is Deprecated"))
        .AddColumn(TableColumn("Dependency Count"))
        .AddColumn(TableColumn("License"))
        .AddColumn(TableColumn("Versions"))
        .AddColumn(TableColumn("Maintainers"))
        .AddColumn(TableColumn("Last Update"))

    let maintainers =
      package.maintainers
      |> Seq.truncate 5
      |> Seq.map (fun maintainer ->
        $"[yellow]{maintainer.name}[/] - [yellow]{maintainer.email}[/]")

    let maintainers = String.Join("\n", maintainers)

    let versions =
      package.distTags
      |> Map.toSeq
      |> Seq.truncate 5
      |> Seq.map (fun (name, version) ->
        $"[bold yellow]{name}[/] - [dim green]{version}[/]")

    let versions = String.Join("\n", versions)

    let deprecated =
      if package.isDeprecated then
        "[bold red]Yes[/]"
      else
        "[green]No[/]"

    let sep =
      Rule($"[bold green]{package.name}[/]").Centered().RuleStyle("bold green")

    AnsiConsole.Write sep

    table.AddRow(
      Markup(package.description),
      Markup(deprecated),
      Markup($"[bold green]%i{package.dependenciesCount}[/]"),
      Markup($"[bold yellow]{package.license}[/]"),
      Markup(versions),
      Markup(maintainers),
      Markup(package.updatedAt.ToShortDateString())
    )
    |> ignore

    AnsiConsole.Write table

  let Search (name: string, page: int) =
    task {
      let! results =
        Logger.spinner (
          "Searching for package information",
          Skypack.SearchPackage(name, page)
        )

      results.results |> printSearchTable

      Logger.log (
        $"[bold green]Found[/]: {results.meta.totalCount}",
        escape = false
      )

      Logger.log (
        $"[bold green]Page[/] {results.meta.page} of {results.meta.totalPages}",
        escape = false
      )
    }

  let Show (name: string) =
    task {
      let! package =
        Logger.spinner (
          "Searching for package information",
          Skypack.PackageInfo name
        )

      printShowTable package
    }

  let consolidateResolutions
    (
      packages: (string * string) list,
      newMap: ImportMap
    ) : Map<string, string> =

    let unparsablePackages =
      packages
      |> List.choose (fun (name, url) ->
        match ExtractDependencyInfoFromUrl url with
        | Some _ -> None
        | None -> Some(name, url))

    if unparsablePackages |> List.length > 0 then
      Logger.log
        "Unable to get standard package information from following packages:"

      Logger.log "These are likely to be manual resolutions"

      Logger.log
        "f there's an actual dependency from the supported providers here please report it as a bug"

      for name, url in unparsablePackages do
        Logger.log $"[yellow]{name}[/yellow] - {url}"

    let allPackages =
      let unresolved = unparsablePackages

      unresolved
      |> List.fold
           (fun current (uName, uUrl) -> current |> Map.add uName uUrl)
           newMap.imports

    allPackages

type Dependencies =

  static member Add
    (
      package: string,
      map: ImportMap,
      provider: Provider,
      [<Optional>] ?runConfig: RunConfiguration
    ) =
    taskResult {
      let! resultMap =
        PackageManager.AddJspm(
          package,
          [ GeneratorEnv.Browser
            GeneratorEnv.Module
            match runConfig with
            | Some RunConfiguration.Production -> GeneratorEnv.Production
            | Some RunConfiguration.Development
            | None -> GeneratorEnv.Production ],
          map,
          provider
        )

      let packages = map.imports |> Map.toList

      let allPackages =
        Dependencies.consolidateResolutions (packages, resultMap)

      return { resultMap with imports = allPackages }
    }

  static member Restore
    (
      package: string,
      [<Optional>] ?provider: Provider,
      [<Optional>] ?runConfig: RunConfiguration
    ) =
    PackageManager.AddJspm(
      package,
      [ GeneratorEnv.Browser
        GeneratorEnv.Module
        match runConfig with
        | Some RunConfiguration.Production -> GeneratorEnv.Production
        | Some RunConfiguration.Development
        | None -> GeneratorEnv.Production ],
      ?provider = provider
    )

  static member Restore
    (
      packages: string seq,
      ?provider: Provider,
      ?runConfig: RunConfiguration
    ) =
    PackageManager.AddJspm(
      packages,
      [ GeneratorEnv.Browser
        GeneratorEnv.Module
        match runConfig with
        | Some RunConfiguration.Production -> GeneratorEnv.Production
        | Some RunConfiguration.Development
        | None -> GeneratorEnv.Production ],
      ?provider = provider
    )

  static member GetMapAndDependencies
    (
      packages: string seq,
      [<Optional>] ?provider: Provider,
      [<Optional>] ?runConfig: RunConfiguration
    ) =
    PackageManager.Regenerate(
      packages,
      [ GeneratorEnv.Browser
        GeneratorEnv.Module
        match runConfig with
        | Some RunConfiguration.Production -> GeneratorEnv.Production
        | Some RunConfiguration.Development
        | None -> GeneratorEnv.Production ],
      ?provider = provider
    )
    |> TaskResult.map (fun result -> result.staticDeps, result.map)

  static member GetMapAndDependencies
    (
      map: ImportMap,
      [<Optional>] ?provider: Provider,
      [<Optional>] ?runConfig: RunConfiguration
    ) =
    let packages = map.imports |> Map.toList

    let parsablePackages =
      packages
      |> List.choose (snd >> ExtractDependencyInfoFromUrl)
      |> List.map (fun (_, name, version) -> $"{name}@{version}")

    PackageManager.Regenerate(
      parsablePackages,
      [ GeneratorEnv.Browser
        GeneratorEnv.Module
        match runConfig with
        | Some RunConfiguration.Production -> GeneratorEnv.Production
        | Some RunConfiguration.Development
        | None -> GeneratorEnv.Production ],
      importMap = map,
      ?provider = provider
    )
    |> TaskResult.map (fun result -> result.staticDeps, result.map)

  static member Remove
    (
      package: string,
      map: ImportMap,
      provider: Provider,
      [<Optional>] ?runConfig: RunConfiguration
    ) =
    taskResult {
      let packages =
        map.imports
        |> Map.filter (fun existing _ -> existing <> package)
        |> Map.toList

      let parsablePackages =
        packages
        |> List.choose (snd >> ExtractDependencyInfoFromUrl)
        |> List.map (fun (_, name, version) -> $"{name}@{version}")

      let! resultMap =
        PackageManager.AddJspm(
          parsablePackages,
          [ GeneratorEnv.Browser
            GeneratorEnv.Module
            match runConfig with
            | Some RunConfiguration.Production -> GeneratorEnv.Production
            | Some RunConfiguration.Development
            | None -> GeneratorEnv.Production ],
          map,
          provider
        )

      let allPackages =
        Dependencies.consolidateResolutions (packages, resultMap)

      return { resultMap with imports = allPackages }
    }

  static member SwitchProvider
    (
      map: ImportMap,
      provider: Provider,
      [<Optional>] ?runConfig: RunConfiguration
    ) =
    taskResult {
      let packages = map.imports |> Map.toList

      let parsablePackages =
        packages
        |> List.choose (snd >> ExtractDependencyInfoFromUrl)
        |> List.map (fun (_, name, version) -> $"{name}@{version}")

      let! resultMap =
        PackageManager.AddJspm(
          parsablePackages,
          [ GeneratorEnv.Browser
            GeneratorEnv.Module
            match runConfig with
            | Some RunConfiguration.Production -> GeneratorEnv.Production
            | Some RunConfiguration.Development
            | None -> GeneratorEnv.Production ],
          provider = provider
        )

      let allPackages =
        Dependencies.consolidateResolutions (packages, resultMap)

      return { resultMap with imports = allPackages }
    }

  static member LocateDependenciesFromMapAndConfig
    (
      importMap: ImportMap,
      config: PerlaConfig
    ) =
    let devDependencies =
      config.devDependencies |> Seq.map (fun f -> f.name) |> set

    let dependencies = config.dependencies |> Seq.map (fun f -> f.name) |> set

    let allTogether = set dependencies |> Set.union (set devDependencies)

    let fromImportMap =
      importMap.imports
      |> Map.toList
      |> List.choose (fun (name, url) ->
        match ExtractDependencyInfoFromUrl url with
        | Some value ->
          if allTogether |> Set.contains name then
            Some value
          else
            None
        | None -> None)
      |> List.map (fun (_, name, version) ->
        { name = name
          version = Some version
          alias = None })

    let deps, devDeps =
      fromImportMap
      |> List.fold
           (fun (current: Set<Dependency> * Set<Dependency>) (next: Dependency) ->
             let deps, devDeps = current

             if dependencies |> Set.contains next.name then
               (deps |> Set.add next, devDeps)
             elif devDependencies |> Set.contains next.name then
               (deps, devDeps |> Set.add next)
             else
               match config.runConfiguration with
               | RunConfiguration.Production -> (deps |> Set.add next, devDeps)
               | RunConfiguration.Development -> (deps, devDeps |> Set.add next))
           (Set.empty<Dependency>, Set.empty<Dependency>)

    seq deps, seq devDeps
