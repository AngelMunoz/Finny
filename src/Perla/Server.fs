namespace Perla.Server

#nowarn "3391"

open System
open System.IO
open System.Net
open System.Net.Http
open System.Net.NetworkInformation
open System.Text
open System.Threading.Tasks
open System.Runtime.CompilerServices

open AngleSharp
open AngleSharp.Html.Parser
open AngleSharp.Io

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Primitives
open Microsoft.AspNetCore.StaticFiles
open Microsoft.Net.Http.Headers

open Yarp.ReverseProxy
open Yarp.ReverseProxy.Forwarder

open FSharp.Control
open FSharp.Control.Reactive

open FsToolkit.ErrorHandling

open Perla
open Perla.Types
open Perla.Units
open Perla.Json
open Perla.Logger
open Perla.Plugins
open Perla.FileSystem
open Perla.VirtualFs

open FSharp.UMX

open Hellang.Middleware.SpaFallback

module Types =

  [<RequireQualifiedAccess; Struct>]
  type ReloadKind =
    | FullReload
    | HMR


  [<RequireQualifiedAccess; Struct>]
  type PerlaScript =
    | LiveReload
    | Worker
    | Env

  [<RequireQualifiedAccess>]
  type ReloadEvents =
    | FullReload of string
    | ReplaceCSS of string
    | CompileError of string

    member this.AsString =
      match this with
      | FullReload data -> $"event:reload\ndata:{data}\n\n"
      | ReplaceCSS data -> $"event:replace-css\ndata:{data}\n\n"
      | CompileError err -> $"event:compile-err\ndata:{err}\n\n"

open Types

[<AutoOpen>]
module Extensions =

  /// Taken from https://github.com/giraffe-fsharp/Giraffe/blob/71ef664f7a6276b1f7cc548189c54dccf633898c/src/Giraffe/HttpContextExtensions.fs#L21
  /// Licensed under: https://github.com/giraffe-fsharp/Giraffe/blob/71ef664f7a6276b1f7cc548189c54dccf633898c/LICENSE
  [<Extension>]
  type HttpContextExtensions() =

    /// <summary>
    /// Gets an instance of `'T` from the request's service container.
    /// </summary>
    /// <returns>Returns an instance of `'T`.</returns>
    [<Extension>]
    static member GetService<'T>(ctx: HttpContext) =
      let t = typeof<'T>

      match ctx.RequestServices.GetService t with
      | null -> raise (exn t.Name)
      | service -> service :?> 'T

    /// <summary>
    /// Gets an instance of <see cref="Microsoft.Extensions.Logging.ILogger{T}" /> from the request's service container.
    ///
    /// The type `'T` should represent the class or module from where the logger gets instantiated.
    /// </summary>
    /// <returns> Returns an instance of <see cref="Microsoft.Extensions.Logging.ILogger{T}" />.</returns>
    [<Extension>]
    static member GetLogger<'T>(ctx: HttpContext) =
      ctx.GetService<ILogger<'T>>()

    /// <summary>
    /// Gets an instance of <see cref="Microsoft.Extensions.Logging.ILogger"/> from the request's service container.
    /// </summary>
    /// <param name="ctx">The current http context object.</param>
    /// <param name="categoryName">The category name for messages produced by this logger.</param>
    /// <returns>Returns an instance of <see cref="Microsoft.Extensions.Logging.ILogger"/>.</returns>
    [<Extension>]
    static member GetLogger(ctx: HttpContext, categoryName: string) =
      let loggerFactory = ctx.GetService<ILoggerFactory>()
      loggerFactory.CreateLogger categoryName

    /// <summary>
    /// Sets the HTTP status code of the response.
    /// </summary>
    /// <param name="ctx">The current http context object.</param>
    /// <param name="httpStatusCode">The status code to be set in the response. For convenience you can use the static <see cref="Microsoft.AspNetCore.Http.StatusCodes"/> class for passing in named status codes instead of using pure int values.</param>
    [<Extension>]
    static member SetStatusCode(ctx: HttpContext, httpStatusCode: int) =
      ctx.Response.StatusCode <- httpStatusCode

    /// <summary>
    /// Adds or sets a HTTP header in the response.
    /// </summary>
    /// <param name="ctx">The current http context object.</param>
    /// <param name="key">The HTTP header name. For convenience you can use the static <see cref="Microsoft.Net.Http.Headers.HeaderNames"/> class for passing in strongly typed header names instead of using pure `string` values.</param>
    /// <param name="value">The value to be set. Non string values will be converted to a string using the object's ToString() method.</param>
    [<Extension>]
    static member SetHttpHeader(ctx: HttpContext, key: string, value: obj) =
      ctx.Response.Headers.[key] <- StringValues(value.ToString())

    /// <summary>
    /// Sets the Content-Type HTTP header in the response.
    /// </summary>
    /// <param name="ctx">The current http context object.</param>
    /// <param name="contentType">The mime type of the response (e.g.: application/json or text/html).</param>
    [<Extension>]
    static member SetContentType(ctx: HttpContext, contentType: string) =
      ctx.SetHttpHeader(HeaderNames.ContentType, contentType)

    /// <summary>
    /// Writes a byte array to the body of the HTTP response and sets the HTTP Content-Length header accordingly.
    /// </summary>
    /// <param name="ctx">The current http context object.</param>
    /// <param name="bytes">The byte array to be send back to the client.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    [<Extension>]
    static member WriteBytesAsync(ctx: HttpContext, bytes: byte[]) =
      task {
        ctx.SetHttpHeader(HeaderNames.ContentLength, bytes.Length)

        if ctx.Request.Method <> HttpMethods.Head then
          do! ctx.Response.Body.WriteAsync(bytes, 0, bytes.Length)

        return Some ctx
      }

    /// <summary>
    /// Writes an UTF-8 encoded string to the body of the HTTP response and sets the HTTP Content-Length header accordingly.
    /// </summary>
    /// <param name="ctx">The current http context object.</param>
    /// <param name="str">The string value to be send back to the client.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    [<Extension>]
    static member WriteStringAsync(ctx: HttpContext, str: string) =
      ctx.WriteBytesAsync(Encoding.UTF8.GetBytes str)

    /// <summary>
    /// Writes an UTF-8 encoded string to the body of the HTTP response and sets the HTTP `Content-Length` header accordingly, as well as the `Content-Type` header to `text/plain`.
    /// </summary>
    /// <param name="ctx">The current http context object.</param>
    /// <param name="str">The string value to be send back to the client.</param>
    /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
    [<Extension>]
    static member WriteTextAsync(ctx: HttpContext, str: string) =
      ctx.SetContentType "text/plain; charset=utf-8"
      ctx.WriteStringAsync str


module LiveReload =

  let WriteReloadChange (event: FileChangedEvent, response: HttpResponse) =
    let data =
      Json.ToText(
        {| oldName = event.oldName
           name = event.name |},
        true
      )

    Logger.log ($"LiveReload: File Changed: {event.name}", target = Serve)
    response.WriteAsync $"event:reload\ndata:{data}\n\n"

  let WriteHmrChange
    (
      event: FileChangedEvent,
      transform: FileTransform,
      response: HttpResponse
    ) =
    let data =
      Json.ToText(
        {| oldName =
            if transform.extension = ".css" then event.oldName else None
           name =
            if Env.IsWindows then
              (UMX.untag event.path).Replace(Path.DirectorySeparatorChar, '/')
            else
              UMX.untag event.path
           content = transform.content |},
        true
      )

    Logger.log ($"HMR: CSS File Changed: {event.name}", target = Serve)
    response.WriteAsync $"event:replace-css\ndata:{data}\n\n"

  let WriteCompileError (error: string option, response: HttpResponse) =
    let err = Json.ToText({| error = error |}, true)
    Logger.log ($"Compilation Error: {err.Substring(0, 80)}...", target = Serve)
    response.WriteAsync(ReloadEvents.CompileError(err).AsString)


[<RequireQualifiedAccess>]
module Middleware =

  [<Struct>]
  type RequestedAs =
    | ModuleAssertion
    | JS
    | Normal

  let processFile
    (
      setContentAndWrite: string * string -> Task<_>,
      reqPath: string,
      mimeType: string,
      requestedAs: RequestedAs,
      content: string
    ) : Task =
    let processCssAsJs (content, url: string) =
      $"""const style=document.createElement('style');style.setAttribute("filename", "{url}");
document.head.appendChild(style).innerHTML=`{content}`;"""

    let processJsonAsJs (content) = $"""export default {content};"""

    match mimeType, requestedAs with
    | "application/json", RequestedAs.JS ->
      setContentAndWrite (
        MimeTypeNames.DefaultJavaScript,
        processJsonAsJs content
      )
    | "text/css", Normal ->
      setContentAndWrite (
        MimeTypeNames.DefaultJavaScript,
        processCssAsJs (content, reqPath)
      )
    | mimeType, ModuleAssertion
    | mimeType, Normal -> setContentAndWrite (mimeType, content)
    | mimeType, value ->
      Logger.log (
        $"Requested %A{value} - {mimeType} - {reqPath} as JS, this file type is not supported as JS, sending default content"
      )

      setContentAndWrite (mimeType, content)

  let ResolveFile
    (config: PerlaConfig)
    : HttpContext -> RequestDelegate -> Task =
    fun ctx next ->
      task {
        match
          VirtualFileSystem.TryResolveFile(UMX.tag<ServerUrl> ctx.Request.Path)
        with
        | Some file ->
          let fileExtProvider =
            ctx.GetService<FileExtensionContentTypeProvider>()

          match fileExtProvider.TryGetContentType(ctx.Request.Path) with
          | true, mime ->
            let setContentTypeAndWrite (mimeType, content) =
              ctx.SetContentType mimeType
              ctx.WriteStringAsync content

            let requestedAs =
              let query = ctx.Request.Query

              let isModule =
                ctx.Request.Path.HasValue
                && ctx.Request.Path.Value.Contains(".module.")

              if query.ContainsKey("module") || isModule then JS
              elif query.ContainsKey("assertion") then ModuleAssertion
              else Normal

            return!
              processFile (
                setContentTypeAndWrite,
                ctx.Request.Path,
                mime,
                requestedAs,
                file
              )
          | false, _ -> return! next.Invoke(ctx)
        | None -> return! next.Invoke(ctx)
      }
      :> Task


  let SendScript (script: PerlaScript) (ctx: HttpContext) =
    task {
      let logger = ctx.GetLogger("Perla:")

      logger.LogInformation($"Sending Script %A{script}")

      match script with
      | PerlaScript.LiveReload ->
        return
          Results.Text(FileSystem.LiveReloadScript.Value, "text/javascript")

      | PerlaScript.Worker ->
        return Results.Text(FileSystem.WorkerScript.Value, "text/javascript")

      | PerlaScript.Env ->
        match Env.GetEnvContent() with
        | Some content ->
          return
            Results.Stream(
              new MemoryStream(Encoding.UTF8.GetBytes content),
              "text/javascript"
            )
        | None ->
          logger.LogWarning(
            "An env file was requested but no env variables were found"
          )

          let message =
            """If you want to use env variables, remember to prefix them with 'PERLA_' e.g.
'PERLA_myApiKey' or 'PERLA_CLIENT_SECRET', then you will be able to import them via the env file"""

          return Results.NotFound({| message = message |})
    }

  let SseHandler
    (eventStream: IObservable<FileChangedEvent * FileTransform>)
    (compileErrors: IObservable<string option>)
    (ctx: HttpContext)
    =
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

      let onChangeSub =
        eventStream
        |> Observable.map (fun (event, fileTransform) ->
          task {
            match event.changeType with
            | Changed when fileTransform.extension.ToLowerInvariant() = ".css" ->
              do! LiveReload.WriteHmrChange(event, fileTransform, res)
            | _ -> do! LiveReload.WriteReloadChange(event, res)

            do! res.Body.FlushAsync()
          })
        |> Observable.switchTask
        |> Observable.subscribe (fun _ ->
          logger.LogInformation "File Changed Event processed")

      let onCompilerErrorSub =
        compileErrors
        |> Observable.map (fun error ->
          task {
            do! LiveReload.WriteCompileError(error, res)
            do! res.Body.FlushAsync()
          })
        |> Observable.switchTask
        |> Observable.subscribe (fun _ ->
          logger.LogWarning "Compile Error Event processed")

      ctx.RequestAborted.Register(fun _ ->
        onChangeSub.Dispose()
        onCompilerErrorSub.Dispose())
      |> ignore

      while true do
        do! Async.Sleep(TimeSpan.FromSeconds 1.)

      return Results.Ok()
    }

  let IndexHandler (config: PerlaConfig) (ctx: HttpContext) =
    let logger = ctx.GetLogger("Perla")
    let content = FileSystem.IndexFile(config.index)
    let map = FileSystem.ImportMap()

    let context = BrowsingContext.New(Configuration.Default)

    let parser = context.GetService<IHtmlParser>()
    let doc = parser.ParseDocument content

    let script = doc.CreateElement "script"
    script.SetAttribute("type", "importmap")
    script.TextContent <- Json.ToText map
    doc.Head.AppendChild script |> ignore

    if config.devServer.liveReload then
      let liveReload = doc.CreateElement "script"
      liveReload.SetAttribute("type", MimeTypeNames.DefaultJavaScript)
      liveReload.SetAttribute("src", "/~perla~/livereload.js")
      doc.Body.AppendChild liveReload |> ignore

    Results.Text(doc.ToHtml(), MimeTypeNames.Html)

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

  let isAddressPortOccupied (address: string) (port: int) =
    let didParse, address = IPEndPoint.TryParse($"{address}:{port}")

    if didParse then
      let props = IPGlobalProperties.GetIPGlobalProperties()

      let listeners = props.GetActiveTcpListeners()

      listeners
      |> Array.map (fun listener -> listener.Port)
      |> Array.contains address.Port
    else
      false

  let getHttpClientAndForwarder () =
    let socketsHandler = new SocketsHttpHandler()
    socketsHandler.UseProxy <- false
    socketsHandler.AllowAutoRedirect <- false
    socketsHandler.AutomaticDecompression <- DecompressionMethods.None
    socketsHandler.UseCookies <- false
    let client = new HttpMessageInvoker(socketsHandler)

    let reqConfig =
      ForwarderRequestConfig(ActivityTimeout = TimeSpan.FromSeconds(100.))

    client, reqConfig


type Server =
  static member GetServerApp
    (
      config: PerlaConfig,
      fileChangedEvents: IObservable<FileChangedEvent * FileTransform>,
      compileErrorEvents: IObservable<string option>
    ) =

    let builder = WebApplication.CreateBuilder()

    let proxy = config.devServer.proxy

    let host = config.devServer.host
    let port = config.devServer.port
    let useSSL = config.devServer.useSSL
    let liveReload = config.devServer.liveReload
    let enableEnv = config.enableEnv
    let envPath = config.envPath

    let http, https =
      match Server.isAddressPortOccupied host port with
      | false ->
        if useSSL then
          $"http://{host}:{port - 1}", $"https://{host}:{port}"
        else
          $"http://{host}:{port}", $"https://{host}:{port + 1}"
      | true ->
        Logger.log (
          $"Address {host}:{port} is busy, selecting a dynamic port.",
          target = PrefixKind.Serve
        )

        $"http://{host}:{0}", $"https://{host}:{0}"

    if proxy |> Map.isEmpty |> not then
      builder.Services.AddHttpForwarder() |> ignore

    builder.Services.AddSpaFallback() |> ignore

    builder.Services.AddSingleton<FileExtensionContentTypeProvider>(fun _ ->
      new FileExtensionContentTypeProvider())
    |> ignore

    let app = builder.Build()

    app.Urls.Add(http)
    app.Urls.Add(https)

    if enableEnv then
      app.MapGet(
        UMX.untag envPath,
        Func<HttpContext, Task<IResult>>(Middleware.SendScript PerlaScript.Env)
      )
      |> ignore

    app.MapGet("/", Func<HttpContext, IResult>(Middleware.IndexHandler config))
    |> ignore

    app.MapGet(
      "/index.html",
      Func<HttpContext, IResult>(Middleware.IndexHandler config)
    )
    |> ignore

    if liveReload then
      app.MapGet(
        "/~perla~/sse",
        Func<HttpContext, Task<IResult>>(
          Middleware.SseHandler fileChangedEvents compileErrorEvents
        )
      )
      |> ignore

      app.MapGet(
        "/~perla~/livereload.js",
        Func<HttpContext, Task<IResult>>(
          Middleware.SendScript PerlaScript.LiveReload
        )
      )
      |> ignore

      app.MapGet(
        "/~perla~/worker.js",
        Func<HttpContext, Task<IResult>>(
          Middleware.SendScript PerlaScript.Worker
        )
      )
      |> ignore

    if useSSL then
      app.UseHsts().UseHttpsRedirection() |> ignore

    app.UseSpaFallback() |> ignore

    match proxy |> Map.isEmpty with
    | false ->
      app
        .UseRouting()
        .UseEndpoints(fun endpoints ->
          let client, reqConfig = Server.getHttpClientAndForwarder ()

          for from, target in proxy |> Map.toSeq do
            let handler = Middleware.getProxyHandler target client reqConfig
            endpoints.Map(from, handler) |> ignore)
      |> ignore
    | true -> ()

    app.Use(
      Func<HttpContext, RequestDelegate, Task>(Middleware.ResolveFile config)
    )
    |> ignore

    app
