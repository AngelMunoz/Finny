namespace Nacre

open System
open System.IO
open System.Text.Json

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting

open Microsoft.Extensions.FileProviders
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

open Giraffe

open Nacre
open Nacre.Types

[<RequireQualifiedAccess>]
module TestLogger =

    let private logError (error: TestError) =

        printfn $"{error.name}: [Actual: {error.actual} - Expected: {error.expected}]"
        printfn $"\t{error.message}"
        printfn "\t%s" error.stack

    let private logTest (test: Test) =
        printfn $"{test.name} - [Passed: {test.passed} {test.duration}ms]"

        match test.error with
        | ValueNone -> ()
        | ValueSome error -> logError error

    let rec private logSuite (suite: Suite) =
        let tests = defaultArg suite.tests [||]
        let suites = defaultArg suite.suites [||]

        for suite in suites do
            logSuite suite

        suite.name |> Option.iter (printfn "Suite: %s")

        for test in tests do
            logTest test

    let private logExecutionResult (result: ExecutionResult ValueOption) =
        match result with
        | ValueSome result ->
            printfn $"Passed: {result.passed}"
            logSuite result.testResults

            for error in result.errors do
                logError error
        | ValueNone -> ()

    let LogMessage (payload: SendMessagePayload) =
        let agent = defaultArg payload.userAgent ""

        if not <| String.IsNullOrWhiteSpace agent then
            printfn $"Runnning Tests in {agent}:"
            logExecutionResult payload.result
            logExecutionResult payload.testResults
        else
            printfn "%A" payload

module Server =

    let private clientHandler (suite: string array * Map<string, string array>) _ (ctx: HttpContext) =
        task {
            let path =
                Path.Combine(Path.NacreExecPath, "test.tpl.html")

            let! content = File.ReadAllTextAsync path
            let tpl = Scriban.Template.Parse content
            let standalone, grouped = suite

            let! result =
                tpl.RenderAsync(
                    {| standalone = standalone
                       grouped = grouped
                       runtimeConfig =
                        {| testFile = None
                           watch = false
                           debug = true
                           host = ctx.Connection.RemoteIpAddress
                           port = ctx.Connection.RemotePort
                           testFrameworkConfig = None |} |}
                )

            return! ctx.WriteHtmlStringAsync(result)
        }

    let private socketScriptHandler _ (ctx: HttpContext) =
        task {
            ctx.SetContentType("text/javascript")

            let path =
                Path.Combine(Path.NacreExecPath, "mock.js")

            return! ctx.WriteFileStreamAsync(false, path, None, None)
        }

    let private clientMessageHandler _ (ctx: HttpContext) =
        task {
            let! msg = JsonSerializer.DeserializeAsync<SendMessagePayload>(ctx.Request.Body)
            TestLogger.LogMessage msg
            return! ctx.WriteJsonAsync({|  |})
        }

    let private getWebApp (suite: string array * Map<string, string array>) : HttpHandler =
        choose [ GET
                 >=> choose [ route "/" >=> (clientHandler suite)
                              route "/__web-dev-server__web-socket.js"
                              >=> socketScriptHandler ]
                 POST
                 >=> route "/~nacre~/messages"
                 >=> clientMessageHandler ]


    let getApp (serveDirectory: string) (suite: string array * Map<string, string array>) =
        let builder = WebApplication.CreateBuilder([||])

        builder.Host.ConfigureLogging (fun (logging: ILoggingBuilder) ->
            logging.SetMinimumLevel(LogLevel.Warning)
            |> ignore)
        |> ignore

        builder.WebHost.UseUrls("http://127.0.0.1:0")
        |> ignore

        builder.Services.AddGiraffe() |> ignore


        let app = builder.Build()
        app.UseGiraffe(getWebApp suite)

        let staticOpts =
            let opts = StaticFileOptions()

            let fileProvider =
                new PhysicalFileProvider(Path.GetFullPath(serveDirectory))

            opts.FileProvider <- fileProvider
            opts

        app.UseStaticFiles(staticOpts) |> ignore
        app
