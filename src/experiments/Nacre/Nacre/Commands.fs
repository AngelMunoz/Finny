namespace Nacre

open System.IO
open System.Threading.Tasks
open Argu
open Microsoft.AspNetCore.Builder
open Nacre
open Nacre.Types
open Nacre.Server


type TestRunnerArgs =
    | Files of string list
    | Groups of string list
    | Browsers of BrowserEnum list
    | All_Files of bool option
    | All_Browsers of bool option
    | Root_Directory of string option
    | Tests_Directory of string option

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Files _ -> "Selects a list of test files to run."
            | Groups _ -> "Selects a group of tests to run."
            | Browsers _ -> "Selects a list of browsers to run the tests against"
            | All_Files _ -> "Sets testing every test file."
            | All_Browsers _ -> "Sets testing in eveCommandsry browser"
            | Root_Directory _ -> "Sets The directory from where to load sources and tests alike."
            | Tests_Directory _ -> "Sets The directory from where to load test files."

    static member ToOptions(args: ParseResults<TestRunnerArgs>) =
        let executionList =
            match args.TryGetResult(All_Files) |> Option.flatten with
            | Some true -> All
            | _ ->
                let files =
                    args.TryGetResult(Files)
                    |> Option.map Array.ofSeq
                    |> Option.defaultValue Array.empty

                let groups =
                    args.TryGetResult(Groups)
                    |> Option.map Array.ofSeq
                    |> Option.defaultValue Array.empty

                ExecutionSuite(files, groups)

        let browsers =
            match args.TryGetResult(All_Browsers) |> Option.flatten with
            | Some true ->
                [| Chromium
                   Chrome
                   Edge
                   Firefox
                   Webkit |]
            | _ ->
                let res =
                    args.TryGetResult(Browsers)
                    |> Option.map Array.ofSeq

                match res with
                | Some [||] -> [| Chromium |]
                | Some value ->
                    value
                    |> Set.ofArray
                    |> Set.map Browser.FromBrowserEnum
                    |> Set.toArray
                | _ -> [| Chromium |]

        let serveDir =
            args.TryGetResult(Root_Directory)
            |> Option.flatten

        let testsDirectory =
            match
                args.TryGetResult(Tests_Directory)
                |> Option.flatten
                with
            | Some directory -> Path.GetFullPath(directory)
            | None -> "./tests"

        { executionList = executionList
          browsers = browsers
          serveDirectory = serveDir
          testsDirectory = testsDirectory }

module Commands =

    open Microsoft.Playwright

    let private getAllScripts rootDir testDir =
        let path = Path.Combine(rootDir, testDir)
        let opts = EnumerationOptions()
        opts.RecurseSubdirectories <- true

        Directory.GetFiles(path, "*.test.js", opts)
        |> Array.Parallel.map (fun path -> path.Replace(rootDir, "./"))

    let private getOnlyTheseScripts rootDir testDir (names: string array) =
        let all = getAllScripts rootDir testDir

        all
        |> Array.filter (fun path ->
            names
            |> Array.exists (fun name -> path.Contains(name)))

    let getScriptsOnlyInTheseDirectories rootDir testDir (directories: string array) =
        let opts = EnumerationOptions()
        opts.RecurseSubdirectories <- true

        seq {
            for directory in directories do
                let path =
                    Path.Combine(rootDir, testDir, directory)

                let files =
                    Directory.GetFiles(path, "*.test.js", opts)
                    |> Array.Parallel.map (fun path -> path.Replace(rootDir, "./"))

                directory, files
        }
        |> Map.ofSeq

    let private startTestRun (getApp: unit -> WebApplication) (browser: Browser) =
        let getBrowser browser (browserOptions: BrowserTypeLaunchOptions) (pl: IPlaywright) =
            match browser with
            | Chromium -> pl.Chromium.LaunchAsync(browserOptions)
            | Chrome ->
                browserOptions.Channel <- "chrome"
                pl.Chromium.LaunchAsync(browserOptions)
            | Edge ->
                browserOptions.Channel <- "msedge"
                pl.Chromium.LaunchAsync(browserOptions)
            | Firefox -> pl.Firefox.LaunchAsync(browserOptions)
            | Webkit -> pl.Webkit.LaunchAsync(browserOptions)

        task {
            let app = getApp ()
            do! app.StartAsync()
            let brOptions = BrowserTypeLaunchOptions()
            brOptions.Devtools <- true
            use! pl = Playwright.CreateAsync()
            use! browser = getBrowser browser brOptions pl
            let! page = browser.NewPageAsync()
            let opts = PageRunAndWaitForConsoleMessageOptions()

            opts.Predicate <-
                fun console ->
                    console.Text = "Nacre:Finished:true"
                    && console.Type = "info"

            let url = app.Urls |> Seq.head
            printfn $"Testing at: {url}"

            do!
                page.RunAndWaitForConsoleMessageAsync(
                    (fun () -> page.GotoAsync $"{url}?wtr-session-id=1" :> Task),
                    opts
                )
                :> Task

            return! app.StopAsync()
        }


    let runTests (opts: TestRunnerOptions) =
        let rootDir = defaultArg opts.serveDirectory "./"

        let testsDir = opts.testsDirectory

        let suite =
            match opts.executionList with
            | All -> (getAllScripts rootDir testsDir, Map.empty)
            | ExecutionSuite (scripts, groups) ->
                let scripts =
                    getOnlyTheseScripts rootDir testsDir scripts

                let groups =
                    getScriptsOnlyInTheseDirectories rootDir testsDir groups

                (scripts, groups)

        task {
            for browser in opts.browsers do
                do! startTestRun (fun _ -> getApp rootDir suite) browser
        }
