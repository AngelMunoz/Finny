namespace Perla.Testing

open System
open System.Threading.Tasks

open Microsoft.Playwright

open Perla
open Perla.Logger
open Perla.Types
open Perla.FileSystem

open FSharp.Control
open FSharp.Control.Reactive

open Spectre.Console
open Spectre.Console.Rendering

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
      suites: Suite list,
      errors: ReportedError list
    ) =
    let getChartItem (color, label, value) =
      { new IBreakdownChartItem with
          member _.Color = color
          member _.Label = label
          member _.Value = value }

    let chart =
      let chart =
        BreakdownChart(ShowTags = true, ShowTagValues = true).FullSize()

      chart.AddItems(
        [ getChartItem (Color.Green, "Tests Passed", stats.passes)
          getChartItem (Color.Red, "Tests Failed", stats.failures) ]
      )

    let endTime = stats.``end`` |> Option.defaultWith (fun _ -> DateTime.Now)
    let difference = endTime - stats.start

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
              $"[yellow] TestRun of {stats.suites} suites and {stats.tests} tests - Duration:[/] [bold yellow]{difference}[/]"
            )
        ) ]

    rows |> Rows |> AnsiConsole.Write

module Testing =
  let SetupPlaywright withDeps =
    Logger.log (
      "[bold yellow]Setting up playwright...[/] This will install [bold cyan]all[/] of the supported browsers",
      escape = false
    )

    try
      let exitCode =
        Program.Main(
          [| "install"
             if withDeps then
               "--with-deps" |]
        )

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
    (events: TestEvent list)
    : TestStats * Suite list * ReportedError list =
    let suiteEnds =
      events
      |> List.choose (fun event ->
        match event with
        | SuiteEnd(_, _, suite) -> Some suite
        | _ -> None)

    let errors =
      events
      |> List.choose (fun event ->
        match event with
        | TestFailed(_, _, test, message, stack) ->
          Some
            { test = Some test
              message = message
              stack = stack }
        | TestImportFailed(_, message, stack) ->
          Some
            { test = None
              message = message
              stack = stack }
        | _ -> None)

    let stats =
      events
      |> List.tryPick (fun event ->
        match event with
        | SessionEnd(_, stats) -> Some stats
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
    (tasks:
      {| allTask: ProgressTask
         failedTask: ProgressTask
         passedTask: ProgressTask |})
    totalTests
    =
    tasks.allTask.MaxValue <- totalTests
    tasks.failedTask.MaxValue <- totalTests
    tasks.passedTask.MaxValue <- totalTests
    tasks.allTask.IsIndeterminate <- true

  let private endSession
    (tasks:
      {| allTask: ProgressTask
         failedTask: ProgressTask
         passedTask: ProgressTask |})
    =
    tasks.failedTask.StopTask()
    tasks.passedTask.StopTask()
    tasks.allTask.StopTask()

  let private passTest
    (tasks:
      {| allTask: ProgressTask
         failedTask: ProgressTask
         passedTask: ProgressTask |})
    =
    tasks.passedTask.Increment(1)
    tasks.allTask.Increment(1)

  let private failTest
    (tasks:
      {| allTask: ProgressTask
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
    (suites: _ list)
    (errors: _ list)
    =
    Console.Clear()
    AnsiConsole.Clear()
    Print.Report(overallStats, suites, errors)

  let PrintReportLive (events: IObservable<TestEvent>) =
    AnsiConsole
      .Progress(HideCompleted = false, AutoClear = false)
      .Start(fun ctx ->
        let suites = ResizeArray()
        let errors = ResizeArray()

        let mutable overallStats =
          { suites = 0
            tests = 0
            passes = 0
            pending = 0
            failures = 0
            start = DateTime.Now
            ``end`` = None }

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
          | SessionStart(_, _, totalTests) -> startNotifications totalTests
          | SessionEnd(_, stats) ->
            overallStats <- stats
            endSession tasks
          | TestPass _ -> passTest tasks
          | TestFailed(_, _, test, message, stack) ->
            failTest (test, message, stack)
          | SuiteStart(_, stats, _) -> overallStats <- stats
          | SuiteEnd(_, stats, suite) ->
            overallStats <- stats
            endSuite suite
          | TestImportFailed(_, message, stack) -> failImport (message, stack)
          | TestRunFinished _ ->
            signalEnd overallStats (suites |> Seq.toList) (errors |> Seq.toList)))

  let getBrowser (browser: Browser, headless: bool, pl: IPlaywright) =
    task {
      let options =
        BrowserTypeLaunchOptions(Devtools = not headless, Headless = headless)

      match browser with
      | Browser.Chrome ->
        options.Channel <- "chrome"
        return! pl.Chromium.LaunchAsync(options)
      | Browser.Edge ->
        options.Channel <- "edge"
        return! pl.Chromium.LaunchAsync(options)
      | Browser.Chromium -> return! pl.Chromium.LaunchAsync(options)
      | Browser.Firefox -> return! pl.Firefox.LaunchAsync(options)
      | Browser.Webkit -> return! pl.Webkit.LaunchAsync(options)
    }

  let monitorPageLogs (page: IPage) =
    page.Console
    |> Observable.subscribeSafe (fun e ->
      let getText color =
        $"[bold {color}]{e.Text.EscapeMarkup()}[/]".EscapeMarkup()

      let writeRule () =
        AnsiConsole.Write(
          Rule(
            $"[dim blue]{e.Location}[/]",
            Style = Style.Parse("dim"),
            Alignment = Justify.Right
          )
        )

      match e.Type with
      | Debug -> ()
      | Info ->
        Logger.log (getText "cyan", target = PrefixKind.Browser)
        writeRule ()
      | Err ->
        Logger.log (getText "red", target = PrefixKind.Browser)
        writeRule ()
      | Warning ->
        Logger.log (getText "orange", target = PrefixKind.Browser)
        writeRule ()
      | Clear ->
        let link = $"[link]{e.Location.EscapeMarkup()}[/]"

        Logger.log (
          $"Browser Console cleared at: {link.EscapeMarkup()}",
          target = PrefixKind.Browser
        )

        writeRule ()
      | _ ->
        Logger.log ($"{e.Text.EscapeMarkup()}", target = PrefixKind.Browser)
        writeRule ()

    )


open Testing

type Testing =

  static member GetBrowser(pl: IPlaywright, browser: Browser, headless: bool) =
    task {
      let options =
        BrowserTypeLaunchOptions(Devtools = false, Headless = headless)

      match browser with
      | Browser.Chrome ->
        options.Channel <- "chrome"
        return! pl.Chromium.LaunchAsync(options)
      | Browser.Edge ->
        options.Channel <- "edge"
        return! pl.Chromium.LaunchAsync(options)
      | Browser.Chromium -> return! pl.Chromium.LaunchAsync(options)
      | Browser.Firefox -> return! pl.Firefox.LaunchAsync(options)
      | Browser.Webkit -> return! pl.Webkit.LaunchAsync(options)
    }

  static member GetExecutor(url: string, browser: Browser) =
    fun (iBrowser: IBrowser) ->
      task {
        let! page =
          iBrowser.NewPageAsync(BrowserNewPageOptions(IgnoreHTTPSErrors = true))

        use _ = Testing.monitorPageLogs page

        do! page.GotoAsync url :> Task

        Logger.log (
          $"Starting session for {browser.AsString}: {iBrowser.Version}",
          target = PrefixKind.Browser
        )

        do!
          page.WaitForConsoleMessageAsync(
            PageWaitForConsoleMessageOptions(
              Predicate =
                (fun event -> event.Text = "__perla-test-run-finished")
            )
          )
          :> Task

        return! page.CloseAsync(PageCloseOptions(RunBeforeUnload = false))
      }

  static member GetLiveExecutor
    (
      url: string,
      browser: Browser,
      fileChanges: IObservable<unit>
    ) =
    fun (iBrowser: IBrowser) ->
      task {
        let! page =
          iBrowser.NewPageAsync(BrowserNewPageOptions(IgnoreHTTPSErrors = true))

        let monitor = Testing.monitorPageLogs page

        do! page.GotoAsync url :> Task

        Logger.log (
          $"Starting session for {browser.AsString}: {iBrowser.Version}",
          target = PrefixKind.Browser
        )

        return
          fileChanges
          |> Observable.map (fun _ ->
            page.ReloadAsync() |> Async.AwaitTask |> Async.Ignore)
          |> Observable.switchAsync
          |> Observable.finallyDo (fun _ -> monitor.Dispose())
      }
