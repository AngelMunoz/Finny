namespace Perla.Testing

open System
open Microsoft.Playwright
open Perla.Logger
open Perla.Types
open FSharp.Control
open FSharp.Control.Reactive
open Spectre.Console
open Spectre.Console.Rendering

[<Struct>]
type Browser =
  | Webkit
  | Firefox
  | Chromium
  | Edge
  | Chrome

  static member FromString(value: string) =
    match value.ToLowerInvariant() with
    | "chromium" -> Chromium
    | "chrome" -> Chrome
    | "edge" -> Edge
    | "webkit" -> Webkit
    | "firefox" -> Firefox
    | _ -> Chromium

type ClientTestException(message: string, stack: string) =
  inherit Exception(message)

  override _.StackTrace = stack

type ReportedError =
  { test: Test option
    message: string
    stack: string }


module Print =

  let test (test: Test, error: (string * string) option) : IRenderable list =
    let stateColor =
      match test.state with
      | Some "passed" -> "green"
      | Some "failed" -> "red"
      | _ -> "grey"

    let duration =
      TimeSpan.FromMilliseconds(test.duration |> Option.defaultValue 0.)

    let speedColor =
      match test.speed with
      | Some "slow" -> "orange"
      | Some "medium" -> "yellow"
      | Some "fast" -> "green"
      | _ -> "grey"

    let skipped = if test.pending then "skipped" else ""

    [ Markup(
        $"[bold yellow]{test.fullTitle.EscapeMarkup()}[/] - [bold {stateColor}]{test.state}[/] [{speedColor}]{duration}[/] [dim blue]{skipped}[/]"
      )
      match error with
      | Some error -> ClientTestException(error).GetRenderable()
      | _ -> () ]

  let suite (suite: Suite, includeTests: bool) : IRenderable =
    let skipped = if suite.pending then "skipped" else ""

    let rows: IRenderable list =
      [ Markup($"{suite.title.EscapeMarkup()} - [dim blue]{skipped}[/]")
        if includeTests then
          yield!
            suite.tests
            |> List.map (fun suiteTest -> test (suiteTest, None) |> List.head) ]

    Panel(
      Rows(rows),
      Header = PanelHeader($"[bold yellow]{suite.fullTitle.EscapeMarkup()}[/]")
    )

  let Stats (stats: TestStats) =

    let content =
      let content: IRenderable seq =
        [ Markup($"Started {stats.start}")
          Markup(
            $"[yellow]Total Suites[/] [bold yellow]{stats.suites}[/] - [yellow]Total Tests[/] [bold yellow]{stats.tests}[/]"
          )
          Markup($"[green]Passed Tests[/] [bold green]{stats.passes}[/]")
          Markup($"[red]Failed Tests[/] [bold red]{stats.failures}[/]")
          Markup($"[blue]Skipped Tests[/] [bold blue]{stats.pending}[/]")
          match stats.``end`` with
          | Some endTime -> Markup($"Start {endTime}")
          | None -> () ]

      Rows(content)

    AnsiConsole.Write(
      Panel(content, Header = PanelHeader("[bold white]Test Results[/]"))
    )

type Print =

  static member Test(test: Test, ?error: string * string) =
    Print.test (test, error) |> Rows |> AnsiConsole.Write

  static member Suite(suite: Suite, ?includeTests: bool) =
    Print.suite (suite, defaultArg includeTests false)
    |> Rows
    |> AnsiConsole.Write

  static member Report
    (
      stats: TestStats,
      suites: Suite seq,
      errors: ReportedError seq
    ) =
    let getChartItem (color, label, value) =
      { new IBreakdownChartItem with
          member _.Color = color
          member _.Label = label
          member _.Value = value }

    let chart =
      let chart =
        BreakdownChart(ShowTags = true, ShowTagValues = true)
          .FullSize()
          .ShowPercentage()

      chart.AddItems(
        [ getChartItem (Color.Yellow, "Tests Total", stats.tests)
          getChartItem (Color.Green, "Tests Passed", stats.passes)
          getChartItem (Color.Red, "Tests Failed", stats.failures) ]
      )

    let endTime = stats.``end`` |> Option.defaultWith (fun _ -> DateTime.Now)
    let difference = endTime - stats.start
    let errors = errors |> Seq.toList

    let rows: IRenderable seq =
      [ for suite in suites do
          Print.suite (suite, true)
        if errors.Length > 0 then
          Rule("Test run errors", Style = Style.Parse("bold red"))

          for error in errors do
            let errorMessage =
              match error.test with
              | Some test -> $"{test.fullTitle} -> {error.message}"
              | None -> error.message

            ClientTestException(errorMessage, error.stack).GetRenderable()

          Rule("", Style = Style.Parse("bold red"))
        Panel(
          chart,
          Header =
            PanelHeader(
              $"[yellow] TestRun of {stats.suites} suites - Duration:[/] [bold yellow]{difference}[/]"
            )
        ) ]

    rows |> Rows |> AnsiConsole.Write



module Testing =
  let SetupPlaywright () =
    Logger.log (
      "[bold yellow]Setting up playwright...[/] This will install [bold cyan]all[/] of the supported browsers",
      escape = false
    )

    try
      let exitCode = Program.Main([| "install" |])

      if exitCode = 0 then
        Logger.log (
          "[bold yellow]Playwright setup[/] [bold green]complete[/]",
          escape = false
        )
      else
        Logger.log (
          "[bold red]We couldn't setup Playwright[/]: you may need to set it up manually",
          escape = false
        )

        Logger.log
          "For more information please visit https://playwright.dev/dotnet/docs/browsers"

    with ex ->
      Logger.log (
        "[bold red]We couldn't setup Playwright[/]: you may need to set it up manually",
        ex = ex,
        escape = false
      )

      Logger.log
        "For more information please visit https://playwright.dev/dotnet/docs/browsers"


  let BuildReport
    (events: TestEvent seq)
    : TestStats * Suite seq * ReportedError seq =
    let suiteEnds =
      events
      |> Seq.choose (fun event ->
        match event with
        | SuiteEnd (_, suite) -> Some suite
        | _ -> None)

    let errors =
      events
      |> Seq.choose (fun event ->
        match event with
        | TestFailed (_, test, message, stack) ->
          Some
            { test = Some test
              message = message
              stack = stack }
        | TestImportFailed (message, stack) ->
          Some
            { test = None
              message = message
              stack = stack }
        | _ -> None)

    let stats =
      events
      |> Seq.tryPick (fun event ->
        match event with
        | SessionEnd stats -> Some stats
        | _ -> None)
      |> Option.defaultWith (fun _ ->
        { suites = 0
          tests = 0
          passes = 0
          pending = 0
          failures = 0
          start = DateTime.Now
          ``end`` = None })

    stats, suiteEnds, errors


  let private startNotifications
    (tasks: {| allTask: ProgressTask
               failedTask: ProgressTask
               passedTask: ProgressTask |})
    totalTests
    =
    tasks.allTask.MaxValue <- totalTests
    tasks.failedTask.MaxValue <- totalTests
    tasks.passedTask.MaxValue <- totalTests
    tasks.allTask.IsIndeterminate <- true

  let private endSession
    (tasks: {| allTask: ProgressTask
               failedTask: ProgressTask
               passedTask: ProgressTask |})
    =
    tasks.failedTask.StopTask()
    tasks.passedTask.StopTask()
    tasks.allTask.StopTask()

  let private passTest
    (tasks: {| allTask: ProgressTask
               failedTask: ProgressTask
               passedTask: ProgressTask |})
    =
    tasks.passedTask.Increment(1)
    tasks.allTask.Increment(1)

  let private failTest
    (tasks: {| allTask: ProgressTask
               failedTask: ProgressTask
               passedTask: ProgressTask |})
    (errors: ResizeArray<_>)
    (test, message, stack)
    =
    tasks.failedTask.Increment(1)
    tasks.allTask.Increment(1)

    errors.Add(
      { test = Some test
        message = message
        stack = stack }
    )

  let private endSuite (suites: ResizeArray<_>) suite = suites.Add(suite)

  let private failImport (errors: ResizeArray<_>) (message, stack) =
    errors.Add(
      { test = None
        message = message
        stack = stack }
    )

  let private signalEnd
    (overallStats: TestStats)
    (suites: _ seq)
    (errors: _ seq)
    =
    Console.Clear()
    AnsiConsole.Clear()
    Print.Report(overallStats, suites, errors)

  let PrintReportLive (events: IObservable<TestEvent>) : IDisposable =
    AnsiConsole
      .Progress(HideCompleted = false, AutoClear = false)
      .Start(fun ctx ->
        let suites = ResizeArray()
        let errors = ResizeArray()
        let mutable overallStats = Unchecked.defaultof<_>

        let tasks =
          {| allTask = ctx.AddTask("All Tests")
             failedTask = ctx.AddTask("Tests Failed")
             passedTask = ctx.AddTask("Tests Passed") |}

        let startNotifications = startNotifications tasks

        let failTest = failTest tasks errors
        let endSuite = endSuite suites
        let failImport = failImport errors

        events
        |> Observable.subscribeSafe (fun value ->
          match value with
          | SessionStart (_, totalTests) -> startNotifications totalTests
          | SessionEnd stats ->
            overallStats <- stats
            endSession tasks
          | TestPass _ -> passTest tasks
          | TestFailed (_, test, message, stack) ->
            failTest (test, message, stack)
          | SuiteStart (stats, _) -> overallStats <- stats
          | SuiteEnd (stats, suite) ->
            overallStats <- stats
            endSuite suite
          | TestImportFailed (message, stack) -> failImport (message, stack)
          | TestRunFinished -> signalEnd overallStats suites errors))

type Testing =

  static member GetBrowser(browser: Browser, headless: bool, pl: IPlaywright) =
    task {
      let options =
        BrowserTypeLaunchOptions(Devtools = not headless, Headless = headless)

      match browser with
      | Chrome ->
        options.Channel <- "chrome"
        return! pl.Chromium.LaunchAsync(options)
      | Edge ->
        options.Channel <- "edge"
        return! pl.Chromium.LaunchAsync(options)
      | Chromium -> return! pl.Chromium.LaunchAsync(options)
      | Firefox -> return! pl.Firefox.LaunchAsync(options)
      | Webkit -> return! pl.Webkit.LaunchAsync(options)
    }
