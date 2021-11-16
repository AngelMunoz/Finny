namespace Perla

open System
open System.IO
open System.Diagnostics
open System.Net
open System.Net.NetworkInformation
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
open System.Text

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
          let fileName =
            path.Replace($"{baseName}/", "", StringComparison.InvariantCulture)

          Path.Combine(baseDir, fileName)

        logger.LogInformation("Transforming CSS")

        let! content = File.ReadAllTextAsync(filePath)

        let newContent =
          $"""
const css = `{content}`
const style = document.createElement('style')
style.innerHTML = css
style.setAttribute("filename", "{filePath}");
document.head.appendChild(style)"""

        ctx.SetContentType "text/javascript"
        do! ctx.WriteStringAsync newContent :> Task
    }
    :> Task

  let jsonImport
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

        let baseDir, baseName =
          mountedDirs
          |> Map.filter (fun _ v -> String.IsNullOrWhiteSpace v |> not)
          |> Map.toSeq
          |> Seq.find (fun (_, v) -> path.StartsWith(v))

        let filePath =
          let fileName =
            path.Replace($"{baseName}/", "", StringComparison.InvariantCulture)

          Path.Combine(baseDir, fileName)

        let! content = File.ReadAllTextAsync(filePath)

        let newContent =
          if ctx.Request.Query.ContainsKey "module" then
            logger.LogInformation("Sending JSON Module")
            ctx.SetContentType "text/javascript"
            $"export default {content}"
          else
            logger.LogInformation("Sending JSON File")
            content

        do! ctx.WriteStringAsync newContent :> Task
    }
    :> Task

  let jsImport
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

        let baseDir, baseName =
          mountedDirs
          |> Map.filter (fun _ v -> String.IsNullOrWhiteSpace v |> not)
          |> Map.toSeq
          |> Seq.find (fun (_, v) -> path.StartsWith(v))

        let filePath =
          let fileName =
            path.Replace($"{baseName}/", "", StringComparison.InvariantCulture)

          Path.Combine(baseDir, fileName)

        ctx.SetContentType "text/javascript"

        try
          if Path.GetExtension(filePath) <> ".js" then
            return failwith "Not a JS file, Try looking with another extension."

          let! content = File.ReadAllBytesAsync(filePath)
          do! ctx.WriteBytesAsync content :> Task
        with
        | ex ->
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
    }
    :> Task

  let configureTransformMiddleware
    (config: FdsConfig)
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



module Server =

  let (|Typescript|Javascript|Jsx|Css|Json|Other|) value =
    match value with
    | ".ts"
    | ".tsx" -> Typescript
    | ".js" -> Javascript
    | ".jsx" -> Jsx
    | ".json" -> Json
    | ".css" -> Css
    | _ -> Other value

  type private Script =
    | LiveReload
    | Worker

  let private sendScript (script: Script) next (ctx: HttpContext) =
    task {
      let basePath =
        let assemblyLoc =
          Path.GetDirectoryName(Reflection.Assembly.GetEntryAssembly().Location)

        if String.IsNullOrWhiteSpace assemblyLoc then
          Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)
        else
          assemblyLoc

      printfn "basePath %s" basePath

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

      let onCompileErrSub =
        Fs
          .compileErrWatcher()
          .Subscribe(fun err ->
            let err = Json.ToTextMinified {| error = err |}
            logger.LogWarning $"Compilation Error"

            task {
              do! res.WriteAsync $"event:compile-err\ndata:{err}\n\n"
              return! res.Body.FlushAsync()
            }
            |> Async.AwaitTask
            |> Async.StartImmediate)

      let onChangeSub =
        watcher.FileChanged
        |> Observable.map (fun event ->
          task {
            match Path.GetExtension event.name with
            | Css ->
              let! content = File.ReadAllTextAsync event.path

              let data =
                Json.ToTextMinified(
                  {| oldName =
                      event.oldName
                      |> Option.map (fun value ->
                        match value with
                        | Css -> value
                        | _ -> "")
                     name = event.path
                     content = content |}
                )

              do! res.WriteAsync $"event:replace-css\ndata:{data}\n\n"
              return! res.Body.FlushAsync()
            | Typescript
            | Javascript
            | Jsx
            | Json
            | Other _ ->
              let data =
                Json.ToTextMinified(
                  {| oldName = event.oldName
                     name = event.name |}
                )

              logger.LogInformation $"LiveReload File Changed: {event.name}"

              do! res.WriteAsync $"event:reload\ndata:{data}\n\n"
              return! res.Body.FlushAsync()
          })
        |> Observable.switchTask
        |> Observable.subscribe ignore

      ctx.RequestAborted.Register (fun _ ->
        watcher.Dispose()
        onChangeSub.Dispose()
        onCompileErrSub.Dispose())
      |> ignore

      while true do
        do! Async.Sleep(TimeSpan.FromSeconds 1.)

      return! text "" next ctx
    }

  let private isAddressPortOccupied (address: string) (port: int) =
    let (didParse, address) = IPEndPoint.TryParse($"{address}:{port}")

    if didParse then
      let props = IPGlobalProperties.GetIPGlobalProperties()

      let listeners = props.GetActiveTcpListeners()

      listeners
      |> Array.map (fun listener -> listener.Port)
      |> Array.contains address.Port
    else
      false


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

        let content = File.ReadAllText(Path.GetFullPath(indexFile))

        let context = BrowsingContext.New(Configuration.Default)

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

    let customHost = defaultArg serverConfig.host "127.0.0.1"
    let customPort = defaultArg serverConfig.port 7331
    let useSSL = defaultArg serverConfig.useSSL false
    let liveReload = defaultArg serverConfig.liveReload true

    let mountedDirs = defaultArg serverConfig.mountDirectories Map.empty

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
        let (http, https) =
          match isAddressPortOccupied customHost customPort with
          | false ->
            $"http://{customHost}:{customPort}",
            $"https://{customHost}:{customPort + 1}"
          | true ->
            printfn
              $"Address {customHost}:{customPort} is Busy, selecting a dynamic port."

            $"http://{customHost}:{0}", $"https://{customHost}:{0}"

        if useSSL then
          config.UseUrls(http, https)
        else
          config.UseUrls(http)

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

  let serverActions
    (tryExecCommand: string -> Async<Result<unit, exn>>)
    (config: FdsConfig)
    (value: string)
    =
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
          try
            stopFable ()
          with
          | ex -> eprintfn "%s" ex.Message

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
          printfn "Couldn't execute command: %s" ex.Message

          printfn
            "Unknown option [%s]\ntype \"exit\" or \"quit\" to finish the application."
            value

          return ()

    }
