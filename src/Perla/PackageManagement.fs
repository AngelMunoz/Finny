namespace Perla

open System
open Spectre.Console

open Perla.Logger
open Perla.PackageManager
open Perla.PackageManager.Types
open Perla.PackageManager.Skypack

open FsToolkit.ErrorHandling


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

type Dependencies =

  static member inline Add
    (
      package: string,
      map: ImportMap,
      provider: Provider
    ) =
    PackageManager.AddJspm(
      package,
      [ GeneratorEnv.Browser; GeneratorEnv.Module; GeneratorEnv.Development ],
      map,
      provider
    )

  static member inline Restore(package: string, ?provider: Provider) =
    PackageManager.AddJspm(
      package,
      [ GeneratorEnv.Browser; GeneratorEnv.Module; GeneratorEnv.Development ],
      ?provider = provider
    )

  static member inline Restore(packages: string seq, ?provider: Provider) =
    PackageManager.AddJspm(
      packages,
      [ GeneratorEnv.Browser; GeneratorEnv.Module; GeneratorEnv.Development ],
      ?provider = provider
    )

  static member inline Remove
    (
      package: string,
      map: ImportMap,
      provider: Provider
    ) =

    PackageManager.Regenerate(
      Map.remove package map.imports |> Map.keys,
      [ GeneratorEnv.Browser; GeneratorEnv.Module; GeneratorEnv.Development ],
      provider
    )

  static member inline SwitchProvider(map: ImportMap, provider: Provider) =
    PackageManager.AddJspm(
      [],
      [ GeneratorEnv.Browser; GeneratorEnv.Module; GeneratorEnv.Development ],
      importMap = map,
      provider = provider
    )
