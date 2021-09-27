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
open Microsoft.Extensions.FileProviders
open Microsoft.AspNetCore.StaticFiles

open FSharp.Control
open FSharp.Control.Reactive
open FSharp.Control.Tasks

open FsToolkit.ErrorHandling

open Giraffe
open Saturn

open CliWrap

open Types
open Fable

[<RequireQualifiedAccess>]
module Middleware =
  let transformPredicate (extensions: string list) (ctx: HttpContext) =
    extensions
    |> List.exists (fun ext -> ctx.Request.Path.Value.Contains(ext))

  let cssImport
    (mountedDirs: Map<string, string>)
    (ctx: HttpContext)
    (next: Func<Task>)
    =
    task {
      if ctx.Request.Path.Value.Contains(".css") |> not then
        return! next.Invoke()
      else

        let logger = ctx.GetLogger("Perla Middleware")
        let path = ctx.Request.Path.Value

        let baseDir, baseName =
          mountedDirs
          |> Map.filter (fun _ v -> String.IsNullOrWhiteSpace v |> not)
          |> Map.toSeq
          |> Seq.find (fun (_, v) -> path.StartsWith(v))

        let filePath =
          let fileName = path.Replace($"{baseName}/", "", StringComparison.InvariantCulture)
          Path.Combine(baseDir, fileName)

        logger.LogInformation("Transforming CSS")

        let! content = File.ReadAllTextAsync(filePath)

        let newContent =
          $"""
const css = `{content}`
const style = document.createElement('style')
style.innerHTML = css
document.head.appendChild(style)"""

        ctx.SetContentType "text/javascript"
        do! ctx.WriteStringAsync newContent :> Task
    }
    :> Task

  let jsImport
    (mountedDirs: Map<string, string>)
    (ctx: HttpContext)
    (next: Func<Task>)
    =
    task {
      let logger = ctx.GetLogger("Perla Middleware")
      if ctx.Request.Path.Value.Contains("~perla~") || ctx.Request.Path.Value.Contains(".js") |> not then
        return! next.Invoke()
      else
        let path = ctx.Request.Path.Value
        logger.LogInformation($"Serving {path}")
        let baseDir, baseName =
          mountedDirs
          |> Map.filter (fun _ v -> String.IsNullOrWhiteSpace v |> not)
          |> Map.toSeq
          |> Seq.find (fun (_, v) -> path.StartsWith(v))

        let filePath =
          let fileName = path.Replace($"{baseName}/", "", StringComparison.InvariantCulture)
          Path.Combine(baseDir, fileName)

        let! content = File.ReadAllBytesAsync(filePath)
        ctx.SetContentType "text/javascript"
        do! ctx.WriteBytesAsync content :> Task
    }
    :> Task

  let configureTransformMiddleware
    (config: FdsConfig)
    (appConfig: IApplicationBuilder)
    =
    let serverConfig =
      defaultArg config.devServer (DevServerConfig.DefaultConfig())

    let mountedDirs =
      defaultArg serverConfig.mountDirectories Map.empty

    appConfig
      .Use(Func<HttpContext, Func<Task>, Task>(cssImport mountedDirs))
      .Use(Func<HttpContext, Func<Task>, Task>(jsImport mountedDirs))
    |> ignore



module Server =
  type private Script =
    | LiveReload
    | Worker

  let private sendScript (script: Script) next (ctx: HttpContext) =
    task {
      let basePath =
        Path.GetDirectoryName(Reflection.Assembly.GetEntryAssembly().Location)

      let! bytes =
        match script with
        | LiveReload ->
          File.ReadAllBytesAsync(Path.Combine(basePath, "./livereload.js"))
        | Worker ->
          File.ReadAllBytesAsync(Path.Combine(basePath, "./worker.js"))

      ctx.SetContentType "text/javascript"
      ctx.SetStatusCode 200
      return! setBody bytes next ctx
    }

  let private Sse (watchConfig: WatchConfig) next (ctx: HttpContext) =
    task {
      let logger = ctx.GetLogger("Perla:SSE")
      logger.LogInformation $"LiveReload Client Connected"
      ctx.SetStatusCode 200
      ctx.SetHttpHeader("Content-Type", "text/event-stream")
      ctx.SetHttpHeader("Cache-Control", "no-cache")
      let res = ctx.Response
      do! res.WriteAsync($"id:{ctx.Connection.Id}\ndata:{DateTime.Now}\n\n")
      do! res.Body.FlushAsync()

      let watcher = Fs.getFileWatcher watchConfig

      logger.LogInformation $"Watching %A{watchConfig.directories} for changes"

      let onChangeSub =
        watcher.FileChanged
        |> Observable.map
             (fun filename ->
               task {
                 let data =
                   Json.ToTextMinified({| filename = filename |})

                 logger.LogInformation $"LiveReload File Changed: {filename}"
                 do! res.WriteAsync $"event:reload\ndata:{data}\n\n"
                 do! res.Body.FlushAsync()
               })
        |> Observable.switchTask
        |> Observable.subscribe ignore

      ctx.RequestAborted.Register
        (fun _ ->
          watcher.Dispose()
          onChangeSub.Dispose())
      |> ignore

      while true do
        do! Async.Sleep(TimeSpan.FromSeconds 1.)

      return! text "" next ctx
    }


  let private Index (next) (ctx: HttpContext) =
    task {
      let logger = ctx.GetLogger("Perla:Index")

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
        let liveReload = doc.CreateElement "script"
        let script = doc.CreateElement "script"
        script.SetAttribute("type", "importmap")
        liveReload.SetAttribute("type", "text/javascript")
        liveReload.SetAttribute("src", "/~perla~/livereload.js")

        match! Fs.getorCreateLockFile (Fs.Paths.GetFdsConfigPath()) with
        | Ok lock ->
          let map: ImportMap =
            { imports = lock.imports
              scopes = lock.scopes }

          script.TextContent <- Json.ToText map
          doc.Head.AppendChild script |> ignore
          doc.Body.AppendChild liveReload |> ignore
          return! htmlString (doc.ToHtml()) next ctx
        | Error err ->

          logger.LogError(
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
    let useSSL = defaultArg serverConfig.useSSL false
    let liveReload = defaultArg serverConfig.liveReload true

    let mountedDirs =
      defaultArg serverConfig.mountDirectories Map.empty

    let watchConfig =
      defaultArg serverConfig.watchConfig (WatchConfig.Default())

    let app =
      let urls =
        if liveReload then
          router {
            get "/" Index
            get "index.html" (redirectTo false "/")
            get "/~perla~/sse" (Sse watchConfig)
            get "/~perla~/livereload.js" (sendScript LiveReload)
            get "/~perla~/worker.js" (sendScript Worker)
          }
        else
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

        let ignoreStatic =
          [ ".js"
            ".css"
            ".module.css"
            ".ts"
            ".tsx"
            ".jsx"
            ".module.json" ]

        for map in mountedDirs do
          let staticFileOptions =
            let provider = FileExtensionContentTypeProvider()

            for ext in ignoreStatic do
              provider.Mappings.Remove(ext) |> ignore

            let options = StaticFileOptions()

            options.ContentTypeProvider <- provider
            options.RequestPath <- PathString(map.Value)

            options.FileProvider <-
              new PhysicalFileProvider(Path.GetFullPath(map.Key))

            options

          appConfig.UseStaticFiles staticFileOptions
          |> ignore

        appConfig.UseWhen(
          Middleware.transformPredicate ignoreStatic,
          Middleware.configureTransformMiddleware config
        )


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
