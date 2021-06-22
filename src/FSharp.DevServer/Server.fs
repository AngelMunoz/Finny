namespace FSharp.DevServer

open Saturn
open Giraffe
open Microsoft.Extensions.Hosting
open System.Threading.Tasks
open System.Threading

module Server =
    let mutable private tokenSource = new CancellationTokenSource()

    let private devServer () =
        let app =
            application { use_router (text "Hello World from Saturn") }

        app.Build()

    let startServer () =
        if tokenSource.IsCancellationRequested then
            tokenSource.Dispose()
            tokenSource <- new CancellationTokenSource()

        let app = devServer ()
        app.StartAsync(tokenSource.Token)

    let stopServer () =
        if tokenSource.IsCancellationRequested |> not then
            tokenSource.Cancel()

    let restartServer () =
        stopServer ()
        startServer ()
