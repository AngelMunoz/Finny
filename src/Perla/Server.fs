namespace Perla

open System
open System.IO
open System.Threading.Tasks

open AngleSharp
open AngleSharp.Html.Parser

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

open FSharp.Control
open FSharp.Control.Tasks

open FsToolkit.ErrorHandling

open Giraffe
open Saturn

open CliWrap

open Types
open Fable
open Microsoft.Extensions.FileProviders

module Server =

  let private Index (next) (ctx: HttpContext) =
    task {
      let logger = ctx.GetLogger()

      match Fs.getFdsConfig (Fs.Paths.GetFdsConfigPath()) with
      | Error err ->
        logger.Log(
          LogLevel.Error,
          "Couldn't append the importmap in the index file",
          err
        )

        return! htmlFile (Path.Combine("./public", "index.html")) next ctx
      | Ok config ->

        let indexFile = defaultArg config.index "index.html"

        let content =
          File.ReadAllText(Path.GetFullPath(indexFile))

        let context =
          BrowsingContext.New(Configuration.Default)

        let parser = context.GetService<IHtmlParser>()
        let doc = parser.ParseDocument content
        let script = doc.CreateElement("script")
        script.SetAttribute("type", "importmap")

        match! Fs.getorCreateLockFile (Fs.Paths.GetFdsConfigPath()) with
        | Ok lock ->
          let map: ImportMap =
            { imports =
                lock
                |> Map.map (fun _ value -> $"{Http.SKYPACK_CDN}{value.pin}")
              scopes = Map.empty }

          script.TextContent <- Json.ToText map
          doc.Head.AppendChild script |> ignore
          logger.LogInformation(doc.ToHtml())
          return! htmlString (doc.ToHtml()) next ctx
        | Error err ->

          logger.Log(
            LogLevel.Error,
            "Couldn't append the importmap in the index file",
            err
          )

          return! htmlFile (Path.GetFullPath(indexFile)) next ctx
    }

  let mutable private app: IHost option = None

  let private startFable =
    let getFableCmd (config: FableConfig option) =
      (fableCmd (Some true) (defaultArg config (FableConfig.DefaultConfig())))
        .WithValidation(CommandResultValidation.None)

    startFable getFableCmd

  let private devServer (config: FdsConfig) =
    let serverConfig =
      defaultArg config.devServer (DevServerConfig.DefaultConfig())

    let customHost = defaultArg serverConfig.host "localhost"
    let customPort = defaultArg serverConfig.port 7331
    let useSSL = defaultArg serverConfig.useSSL true

    let app =
      let urls =
        router {
          get "/" Index
          get "index.html" (redirectTo false "/")
        }

      let withWebhostConfig (config: IWebHostBuilder) =
        if useSSL then
          config.UseUrls(
            $"http://{customHost}:{customPort - 1}",
            $"https://{customHost}:{customPort}"
          )
        else
          config.UseUrls($"http://{customHost}:{customPort}")

      let withAppConfig (appConfig: IApplicationBuilder) =
        if useSSL then
          appConfig.UseHsts().UseHttpsRedirection()
          |> ignore

        for map in
          serverConfig.mountDirectories
          |> Option.defaultValue Map.empty do
          let opts =
            let opts = StaticFileOptions()
            opts.RequestPath <- PathString(map.Value)

            opts.FileProvider <-
              new PhysicalFileProvider(Path.GetFullPath(map.Key))

            opts

          appConfig.UseStaticFiles opts |> ignore

        appConfig

      application {
        app_config withAppConfig
        webhost_config withWebhostConfig
        use_router urls
        use_gzip
      }

    app
      .UseEnvironment(Environments.Development)
      .Build()

  let private stopServer () =
    match app with
    | Some actual ->
      task {
        do! actual.StopAsync()
        actual.Dispose()
        app <- None
      }
      :> Task
    | None -> Task.FromResult(()) :> Task

  let private startServer (config: FdsConfig) =
    match app with
    | None ->
      let dev = devServer config
      app <- Some dev
      task { return! dev.StartAsync() }
    | Some app ->
      task {
        do! stopServer ()
        return! app.StartAsync()
      }

  let serverActions (config: FdsConfig) =
    fun (value: string) ->
      async {
        match value with
        | StartServer ->
          async {
            printfn "Starting Dev Server"
            do! startServer config |> Async.AwaitTask
          }
          |> Async.Start

          return ()
        | StopServer ->
          stopServer () |> Async.AwaitTask |> Async.Start
          return ()
        | RestartServer ->
          async {
            do! stopServer () |> Async.AwaitTask
            printfn "Starting Dev Server"
            do! startServer config |> Async.AwaitTask
          }
          |> Async.Start

          return ()
        | StartFable ->
          async {
            printfn "Starting Fable"

            let! result = startFable config.fable |> Async.AwaitTask
            printfn $"Finished in {result.RunTime}"
          }
          |> Async.Start

          return ()
        | StopFable ->
          printfn "Stoping Fable"
          stopFable ()
        | RestartFable ->
          async {
            printfn "Restarting Fable"

            stopFable ()

            let! result = startFable config.fable |> Async.AwaitTask
            printfn $"Finished in {result.RunTime}"
            return ()
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
        | Unknown value ->
          printfn
            "Unknown option [%s]\ntype \"exit\" or \"quit\" to finish the application."
            value
      }
