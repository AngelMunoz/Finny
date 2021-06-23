namespace FSharp.DevServer

open System
open System.Threading.Tasks

open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting

open FSharp.Control
open FSharp.Control.Tasks

open Giraffe
open Saturn

open CliWrap

open Types
open Fable

module Server =
  let mutable private app : IHost option = None

  let private startFable : FableConfig -> Task<CommandResult> =
    let getFableCmd (config: FableConfig) =
      (fableCmd (Some true) config)
        .WithValidation(CommandResultValidation.None)

    startFable getFableCmd

  let private devServer (config: DevServerConfig) =
    let staticFilesDir =
      defaultArg config.StaticFilesDir "./public"

    let customHost = defaultArg config.Host "localhost"
    let customPort = defaultArg config.Port 7331
    let useSSL = defaultArg config.UseSSL true

    let app =
      let urls =
        router {
          not_found_handler (redirectTo false "/")
          get "/" (htmlFile $"{staticFilesDir}/index.html")
          get "index.html" (redirectTo false "/")
        }

      let withWebhostConfig (config: IWebHostBuilder) =
        config.UseUrls($"http://{customHost}:{customPort}", $"https://{customHost}:{customPort}")

      if useSSL then
        application {
          use_router urls
          webhost_config withWebhostConfig
          use_static staticFilesDir
          use_gzip
          force_ssl
        }
      else
        application {
          use_router urls
          webhost_config withWebhostConfig
          use_static staticFilesDir
          use_gzip
        }

    app.Build()

  let private stopServer () =
    match app with
    | Some app -> app.StopAsync()
    | None -> Task.FromResult(()) :> Task

  let private startServer (config: DevServerConfig) =
    match app with
    | None ->
      let dev = devServer config
      app <- Some dev
      dev.StartAsync()
    | Some app ->
      task {
        do! stopServer ()
        return! app.StartAsync()
      }
      :> Task

  let serverActions (serverConfig: DevServerConfig) (fableConfig: FableConfig) =
    fun (value: string) ->
      async {
        match value with
        | StartServer ->
          async {
            printfn "Starting Dev Server"
            do! startServer serverConfig |> Async.AwaitTask
          }
          |> Async.Start
        | StopServer -> stopServer () |> Async.AwaitTask |> Async.Start
        | RestartServer ->
          async {
            do! stopServer () |> Async.AwaitTask
            printfn "Starting Dev Server"
            do! startServer serverConfig |> Async.AwaitTask
          }
          |> Async.Start
        | StartFable ->
          async {
            printfn "Starting Fable"

            let! result = startFable fableConfig |> Async.AwaitTask
            printfn $"Finished in {result.RunTime}"
          }
          |> Async.Start
        | StopFable ->
          printfn "Stoping Fable"
          stopFable ()
        | RestartFable ->
          async {
            printfn "Restarting Fable"

            stopFable ()

            let! result = startFable fableConfig |> Async.AwaitTask
            printfn $"Finished in {result.RunTime}"
          }
          |> Async.Start
        | Clear -> Console.Clear()
        | Exit ->
          printfn "Finishing the session"

          task {
            stopFable ()
            do! stopServer ()
            exit 0
          }
          |> Async.AwaitTask
          |> Async.StartImmediate
        | UnknownFable value
        | Unknown value -> printfn "Unknown option [%s]" value
      }
