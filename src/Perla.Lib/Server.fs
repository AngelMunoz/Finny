namespace Perla.Lib

open System
open System.IO
open System.Net
open System.Net.Http
open System.Net.NetworkInformation
open System.Text
open System.Threading.Tasks

open AngleSharp
open AngleSharp.Html.Parser
open AngleSharp.Io

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting.Server
open Microsoft.AspNetCore.Hosting.Server.Features
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.Extensions.FileProviders
open Microsoft.AspNetCore.StaticFiles

open Yarp.ReverseProxy
open Yarp.ReverseProxy.Forwarder

open Hellang.Middleware.SpaFallback

open FSharp.Control
open FSharp.Control.Reactive

open FsToolkit.ErrorHandling

open CliWrap

open Perla.Lib
open Types
open Fable
open Logger

module private LiveReload =
  let getReloadEvent (event: Fs.FileChangedEvent) =
    let data =
      Json.ToTextMinified(
        {| oldName = event.oldName
           name = event.name |}
      )

    ReloadKind.FullReload, data

  let getHMREvent (event: Fs.FileChangedEvent) =
    let content = File.ReadAllText event.path

    let data =
      Json.ToTextMinified(
        {| oldName =
            event.oldName
            |> Option.map (fun value ->
              match value with
              | Css -> value
              | _ -> "")
           name =
            if Env.isWindows then
              event.path.Replace(Path.DirectorySeparatorChar, '/')
            else
              event.path
           content = content |}
      )

    ReloadKind.HMR, data

  let getLiveReloadHandler
    isFableEnabled
    (watcher: Fs.IFileWatcher)
    (logger: ILogger)
    (res: HttpResponse)
    =
    let watcher =
      if isFableEnabled then
        watcher.FileChanged
        |> Observable.throttle (TimeSpan.FromMilliseconds(400.))
      else
        watcher.FileChanged

    watcher
    |> Observable.map (fun event ->
      let kind, data =
        match event.ChangeType with
        | Fs.ChangeKind.Created
        | Fs.ChangeKind.Deleted -> getReloadEvent event
        | Fs.ChangeKind.Renamed
        | Fs.ChangeKind.Changed ->
          match Path.GetExtension event.name with
          | Css -> getHMREvent event
          | _ -> getReloadEvent event

      kind, data, event)
    |> Observable.map (fun (kind, data, event) ->
      task {
        match kind with
        | ReloadKind.FullReload ->
          logger.LogInformation $"LiveReload: File Changed: {event.name}"
          do! res.WriteAsync $"event:reload\ndata:{data}\n\n"
        | ReloadKind.HMR ->
          match Path.GetExtension event.name with
          | Css ->
            logger.LogInformation $"HMR: CSS File Changed: {event.name}"
            do! res.WriteAsync $"event:replace-css\ndata:{data}\n\n"
          | other ->
            logger.LogWarning
              $"HMR: {other.ToUpperInvariant()} File Changed: {event.name}"

            logger.LogWarning
              $"There is no HMR handler for this file... Triggering LiveReload"

            do! res.WriteAsync $"event:reload\ndata:{data}\n\n"

        return! res.Body.FlushAsync()
      })
    |> Observable.switchTask
    |> Observable.subscribe ignore

  let getCompileErrHandler (logger: ILogger) (res: HttpResponse) =
    Fs.compileErrWatcher ()
    |> Observable.map (fun err ->
      task {
        let err = Json.ToTextMinified {| error = err |}
        logger.LogWarning $"Compilation Error: {err.Substring(0, 80)}..."
        do! res.WriteAsync(ReloadEvents.CompileError(err).AsString)
        return! res.Body.FlushAsync()
      })
    |> Observable.switchTask
    |> Observable.subscribe ignore


[<RequireQualifiedAccess>]
module private Middleware =
  let tryFindMounted (path: string) mountedDirs =
    mountedDirs
    |> Map.tryPick (fun k v ->
      option {
        let dirname = Path.GetDirectoryName path
        let emptyValue = v = String.Empty
        let rootDir = dirname = @"\" ||  dirname = "/"

        if emptyValue && rootDir then
          return (k, v)
        elif v <> String.Empty && path.StartsWith v then
          return (k, v)
        else
          return! None
      })

  let transformPredicate (extensions: string list) (ctx: HttpContext) =
    extensions
    |> List.exists ctx.Request.Path.Value.Contains

  let private cssImport
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

        match tryFindMounted path mountedDirs with
        | Some (baseDir, baseName) ->
          let filePath =
            let fileName =
              path.Replace(
                $"{baseName}/",
                "",
                StringComparison.InvariantCulture
              )

            Path.Combine(baseDir, fileName)

          logger.LogInformation("Transforming CSS")

          let! content = File.ReadAllTextAsync(filePath)

          if ctx.Request.Query.ContainsKey "module" then
            logger.LogInformation("Sending CSS module")
            ctx.SetContentType MimeTypeNames.Css
            return! ctx.WriteStringAsync content :> Task
          else
            logger.LogInformation("Sending CSS HMR Script")

            let newContent =
              $"""const css = `{content}`,style = document.createElement('style')
style.innerHTML = css;style.setAttribute("filename", "{filePath.Replace(Path.DirectorySeparatorChar, '/')}");
document.head.appendChild(style)"""

            ctx.SetContentType MimeTypeNames.DefaultJavaScript
            return! ctx.WriteStringAsync newContent :> Task
        | None ->
          logger.LogInformation $"Failed to find: {path}"
          ctx.Response.StatusCode <- 404
          return! ctx.Response.CompleteAsync()
    }
    :> Task

  let private jsonImport
    (mountedDirs: Map<string, string>)
    (ctx: HttpContext)
    (next: Func<Task>)
    =
    task {
      if ctx.Request.Path.Value.Contains(".json") |> not then
        return! next.Invoke()
      else

        let logger = ctx.GetLogger("Perla Middleware")
        let path = ctx.Request.Path.Value

        match tryFindMounted path mountedDirs with
        | Some (baseDir, baseName) ->

          let filePath =
            let fileName =
              path.Replace(
                $"{baseName}/",
                "",
                StringComparison.InvariantCulture
              )

            Path.Combine(baseDir, fileName)

          let! content = File.ReadAllTextAsync(filePath)

          let newContent =
            if ctx.Request.Query.ContainsKey "module" then
              logger.LogInformation("Sending JSON Module")
              ctx.SetContentType MimeTypeNames.DefaultJavaScript
              $"export default {content}"
            else
              logger.LogInformation("Sending JSON File")
              content

          do! ctx.WriteStringAsync newContent :> Task
        | None ->
          logger.LogInformation $"Failed to find: {path}"
          ctx.Response.StatusCode <- 404
          return! ctx.Response.CompleteAsync()
    }
    :> Task

  let private jsImport
    (buildConfig: BuildConfig option)
    (mountedDirs: Map<string, string>)
    (ctx: HttpContext)
    (next: Func<Task>)
    =
    task {
      let logger = ctx.GetLogger("Perla Middleware")

      if
        ctx.Request.Path.Value.Contains("~perla~")
        || ctx.Request.Path.Value.Contains(".js") |> not
      then
        return! next.Invoke()
      else
        let path = ctx.Request.Path.Value
        logger.LogInformation($"Serving {path}")

        match tryFindMounted path mountedDirs with
        | Some (baseDir, baseName) ->
          let filePath =
            let fileName =
              path.Replace(
                $"{baseName}/",
                "",
                StringComparison.InvariantCulture
              )

            Path.Combine(baseDir, fileName)

          ctx.SetContentType MimeTypeNames.DefaultJavaScript

          try
            if Path.GetExtension(filePath) <> ".js" then
              return
                failwith "Not a JS file, Try looking with another extension."

            use content = File.OpenRead(filePath)
            do! content.CopyToAsync ctx.Response.Body
          with
          | _ ->
            let! fileData = Esbuild.tryCompileFile filePath buildConfig

            match fileData with
            | Ok (stdout, stderr) ->
              if String.IsNullOrWhiteSpace stderr |> not then
                Fs.PublishCompileErr stderr
                do! ctx.WriteBytesAsync [||] :> Task
              else
                let content = Encoding.UTF8.GetBytes stdout
                do! ctx.WriteBytesAsync content :> Task
            | Error err ->
              ctx.SetStatusCode 500
              do! ctx.WriteTextAsync err.Message :> Task
        | None ->
          logger.LogInformation $"Failed to find: {path}"
          ctx.Response.StatusCode <- 404
          return! ctx.Response.CompleteAsync()
    }
    :> Task

  let configureTransformMiddleware
    (config: PerlaConfig)
    (appConfig: IApplicationBuilder)
    =
    let serverConfig =
      defaultArg config.devServer (DevServerConfig.DefaultConfig())

    let mountedDirs = defaultArg serverConfig.mountDirectories Map.empty

    appConfig
      .Use(Func<HttpContext, Func<Task>, Task>(jsonImport mountedDirs))
      .Use(Func<HttpContext, Func<Task>, Task>(cssImport mountedDirs))
      .Use(
        Func<HttpContext, Func<Task>, Task>(jsImport config.build mountedDirs)
      )
    |> ignore

  let sendScript (script: PerlaScript) (ctx: HttpContext) =
    task {
      let logger = ctx.GetLogger("Perla:")

      let stream =
        match script with
        | PerlaScript.LiveReload ->
          File.OpenRead(
            Path.Combine(Path.PerlaRootDirectory, "./livereload.js")
          )
        | PerlaScript.Worker ->
          File.OpenRead(Path.Combine(Path.PerlaRootDirectory, "./worker.js"))

      logger.LogInformation($"Perla: Sending Script %A{script}")
      return Results.Stream(stream, "text/javascript")
    }

  let SseHandler (watchConfig: WatchConfig) isFableEnabled (ctx: HttpContext) =
    task {
      let logger = ctx.GetLogger("Perla:SSE")
      logger.LogInformation $"LiveReload Client Connected"
      ctx.SetHttpHeader("Content-Type", "text/event-stream")
      ctx.SetHttpHeader("Cache-Control", "no-cache")
      ctx.SetStatusCode 200
      let res = ctx.Response
      // Start Client communication
      do! res.WriteAsync($"id:{ctx.Connection.Id}\ndata:{DateTime.Now}\n\n")
      do! res.Body.FlushAsync()

      let watcher = Fs.getFileWatcher watchConfig

      logger.LogInformation $"Watching %A{watchConfig.directories} for changes"

      let onCompileErrSub = LiveReload.getCompileErrHandler logger res

      let onChangeSub =
        LiveReload.getLiveReloadHandler isFableEnabled watcher logger res

      ctx.RequestAborted.Register (fun _ ->
        watcher.Dispose()
        onChangeSub.Dispose()
        onCompileErrSub.Dispose())
      |> ignore

      while true do
        do! Async.Sleep(TimeSpan.FromSeconds 1.)

      return Results.Ok()
    }

  let IndexHandler (ctx: HttpContext) =
    task {
      let logger = ctx.GetLogger("Perla")

      match Fs.getPerlaConfig (Path.GetPerlaConfigPath()) with
      | Error err ->
        logger.Log(
          LogLevel.Error,
          "Couldn't append the importmap in the index file",
          err
        )

        return
          Results.Stream(
            File.OpenRead(Path.GetFullPath("./index.html")),
            MimeTypeNames.Html
          )
      | Ok config ->

        let indexFile = defaultArg config.index "index.html"

        let content = File.ReadAllText(Path.GetFullPath(indexFile))

        let context = BrowsingContext.New(Configuration.Default)

        let parser = context.GetService<IHtmlParser>()
        let doc = parser.ParseDocument content
        let liveReload = doc.CreateElement "script"
        let script = doc.CreateElement "script"
        script.SetAttribute("type", "importmap")
        liveReload.SetAttribute("type", MimeTypeNames.DefaultJavaScript)
        liveReload.SetAttribute("src", "/~perla~/livereload.js")

        match! Fs.getOrCreateLockFile (Path.GetPerlaConfigPath()) with
        | Ok lock ->
          let map: ImportMap =
            { imports = lock.imports
              scopes = lock.scopes }

          script.TextContent <- Json.ToText map
          doc.Head.AppendChild script |> ignore
          doc.Body.AppendChild liveReload |> ignore
          return Results.Text(doc.ToHtml(), MimeTypeNames.Html)
        | Error err ->

          logger.LogError(
            "Couldn't append the importmap in the index file",
            err
          )

          return
            Results.Stream(
              File.OpenRead(Path.GetFullPath(indexFile)),
              MimeTypeNames.Html
            )
    }

  let getProxyHandler
    (target: string)
    (httpClient: HttpMessageInvoker)
    (forwardConfig: ForwarderRequestConfig)
    : Func<HttpContext, IHttpForwarder, Task> =
    let toFunc (ctx: HttpContext) (forwarder: IHttpForwarder) =
      task {
        let logger = ctx.GetLogger("Perla Proxy")
        let! error = forwarder.SendAsync(ctx, target, httpClient, forwardConfig)

        if error <> ForwarderError.None then
          let errorFeat = ctx.GetForwarderErrorFeature()
          let ex = errorFeat.Exception
          logger.LogWarning($"{ex.Message}")
      }
      :> Task

    Func<HttpContext, IHttpForwarder, Task>(toFunc)

module Server =
  // Local App instance that we use in case we want to kill/restart
  // The server via the interactive commands
  let mutable private app: WebApplication voption = ValueNone

  let private isAddressPortOccupied (address: string) (port: int) =
    let didParse, address = IPEndPoint.TryParse($"{address}:{port}")

    if didParse then
      let props = IPGlobalProperties.GetIPGlobalProperties()

      let listeners = props.GetActiveTcpListeners()

      listeners
      |> Array.map (fun listener -> listener.Port)
      |> Array.contains address.Port
    else
      false

  let private getHttpClientAndForwarder () =
    let socketsHandler = new SocketsHttpHandler()
    socketsHandler.UseProxy <- false
    socketsHandler.AllowAutoRedirect <- false
    socketsHandler.AutomaticDecompression <- DecompressionMethods.None
    socketsHandler.UseCookies <- false
    let client = new HttpMessageInvoker(socketsHandler)
    let reqConfig = ForwarderRequestConfig()
    reqConfig.ActivityTimeout <- TimeSpan.FromSeconds(100.)
    client, reqConfig

  let private startFable =
    let getFableCmd (config: FableConfig option) =
      let logger =
        app
        |> ValueOption.map (fun app ->
          app.Services.GetService<ILogger>()
          |> ValueOption.ofObj)
        |> ValueOption.flatten
        |> ValueOption.defaultValue (Logger.getPerlaLogger ())

      let inline logAddresses () =
        let addresses =
          voption {
            let! app = app

            let! server =
              app.Services.GetService<IServer>()
              |> ValueOption.ofObj

            let! serverAddresses =
              server.Features.Get<IServerAddressesFeature>()
              |> ValueOption.ofObj

            return serverAddresses.Addresses
          }

        match addresses with
        | ValueSome addresses ->
          let value =
            addresses
            |> Seq.reduce (fun current next -> $"\n\t{current}\n\t{next}")

          logger.LogInformation $"Listening at {value}"
        | ValueNone -> ()

      (fableCmd (Some true) (defaultArg config (FableConfig.DefaultConfig())))
        .WithValidation(CommandResultValidation.None)
        .WithStandardOutputPipe(
          PipeTarget.ToDelegate (fun value ->
            if value.ToLowerInvariant().Contains("watching") then
              logger.LogInformation value
              logAddresses ()
            else
              logger.LogInformation value)
        )

    startFable getFableCmd

  let private devServer (config: PerlaConfig) =
    let serverConfig =
      defaultArg config.devServer (DevServerConfig.DefaultConfig())

    let builder = WebApplication.CreateBuilder()

    let getProxyConfig =
      let path = Path.GetProxyConfigPath()
      Fs.getProxyConfig path

    let customHost = defaultArg serverConfig.host "localhost"
    let customPort = defaultArg serverConfig.port 7331
    let useSSL = defaultArg serverConfig.useSSL false
    let liveReload = defaultArg serverConfig.liveReload true

    let mountedDirs = defaultArg serverConfig.mountDirectories Map.empty

    let watchConfig =
      defaultArg serverConfig.watchConfig (WatchConfig.Default())

    let http, https =
      match isAddressPortOccupied customHost customPort with
      | false ->
        if useSSL then
          $"http://{customHost}:{customPort - 1}",
          $"https://{customHost}:{customPort}"
        else
          $"http://{customHost}:{customPort}",
          $"https://{customHost}:{customPort + 1}"
      | true ->
        Logger.serve
          $"Address {customHost}:{customPort} is busy, selecting a dynamic port."

        $"http://{customHost}:{0}", $"https://{customHost}:{0}"


    builder.Services.AddSpaFallback() |> ignore

    getProxyConfig
    |> Option.iter (fun _ -> builder.Services.AddHttpForwarder() |> ignore)

    let app = builder.Build()

    app.Urls.Add(http)
    app.Urls.Add(https)

    app.MapGet("/", Func<HttpContext, Task<IResult>>(Middleware.IndexHandler))
    |> ignore

    app.MapGet(
      "/index.html",
      Func<HttpContext, Task<IResult>>(Middleware.IndexHandler)
    )
    |> ignore

    if liveReload then
      app.MapGet(
        "/~perla~/sse",
        Func<HttpContext, Task<IResult>>(
          Middleware.SseHandler watchConfig (config.fable |> Option.isSome)
        )
      )
      |> ignore

      app.MapGet(
        "/~perla~/livereload.js",
        Func<HttpContext, Task<IResult>>(
          Middleware.sendScript PerlaScript.LiveReload
        )
      )
      |> ignore

      app.MapGet(
        "/~perla~/worker.js",
        Func<HttpContext, Task<IResult>>(
          Middleware.sendScript PerlaScript.Worker
        )
      )
      |> ignore

    if useSSL then
      app.UseHsts().UseHttpsRedirection() |> ignore

    app.UseSpaFallback() |> ignore

    let ignoreStatic =
      [ ".js"
        ".css"
        ".module.css"
        ".ts"
        ".tsx"
        ".jsx"
        ".json" ]

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

      app.UseStaticFiles staticFileOptions |> ignore

    app.UseWhen(
      Middleware.transformPredicate ignoreStatic,
      Middleware.configureTransformMiddleware config
    )
    |> ignore

    match getProxyConfig with
    | Some proxyConfig ->
      app
        .UseRouting()
        .UseEndpoints(fun endpoints ->
          let client, reqConfig = getHttpClientAndForwarder ()

          for from, target in proxyConfig |> Map.toSeq do
            let handler = Middleware.getProxyHandler target client reqConfig
            endpoints.Map(from, handler) |> ignore)
      |> ignore
    | None -> ()

    app

  let private stopServer () =
    match app with
    | ValueSome actual ->
      task {
        do! actual.StopAsync()
        app <- ValueNone
      }
      :> Task
    | ValueNone -> Task.FromResult(()) :> Task

  let private startServer (config: PerlaConfig) =
    match app with
    | ValueNone ->
      let dev = devServer config
      app <- ValueSome dev
      task { return! dev.StartAsync() }
    | ValueSome app ->
      task {
        do! stopServer ()
        return! app.StartAsync()
      }

  let serverActions
    (tryExecCommand: string -> Async<Result<unit, exn>>)
    (getConfig: unit -> PerlaConfig)
    (value: string)
    =
    async {
      match value with
      | StartServer ->
        async {
          Logger.serve "Starting Dev Server"
          do! startServer (getConfig ()) |> Async.AwaitTask
        }
        |> Async.Start

        return ()
      | StopServer ->
        stopServer () |> Async.AwaitTask |> Async.Start
        return ()
      | RestartServer ->
        async {
          do! stopServer () |> Async.AwaitTask
          Logger.serve "Starting Dev Server"
          do! startServer (getConfig ()) |> Async.AwaitTask
        }
        |> Async.Start

        return ()
      | StartFable ->
        async {
          Logger.serve "Starting Fable"

          let! result = startFable (getConfig ()).fable |> Async.AwaitTask
          Logger.serve $"Finished in {result.RunTime}"
        }
        |> Async.Start

        return ()
      | StopFable ->
        Logger.serve "Stoping Fable"
        stopFable ()
      | RestartFable ->
        async {
          Logger.serve "Restarting Fable"

          stopFable ()

          let! result = startFable (getConfig ()).fable |> Async.AwaitTask
          Logger.serve $"Finished in {result.RunTime}"
          return ()
        }
        |> Async.Start
      | Clear -> Console.Clear()
      | Exit ->
        Logger.serve "Finishing the session"

        task {
          try
            stopFable ()
          with
          | ex -> Logger.serve ("Failed to stop fable", ex)

          do! stopServer ()
          exit 0
        }
        |> Async.AwaitTask
        |> Async.StartImmediate
      | UnknownFable value
      | Unknown value ->
        match! tryExecCommand value with
        | Ok () -> return ()
        | Error ex ->
          Logger.serve ("Failed to execute command", ex)

          Logger.serve
            $"Unknown option [{value}]\ntype \"exit\" or \"quit\" to finish the application."

          return ()

    }
