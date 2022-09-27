namespace Perla.Lib


open System
open System.Collections
open System.Text
open FsToolkit.ErrorHandling
open Microsoft.Extensions.Logging
open Perla.Lib
open Types

[<RequireQualifiedAccess>]
module Env =
  open System.Runtime.InteropServices

  let isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)

  let platformString =
    if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
      "windows"
    else if RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
      "linux"
    else if RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
      "darwin"
    else if RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD) then
      "freebsd"
    else
      failwith "Unsupported OS"

  let archString =
    match RuntimeInformation.OSArchitecture with
    | Architecture.Arm -> "arm"
    | Architecture.Arm64 -> "arm64"
    | Architecture.X64 -> "64"
    | Architecture.X86 -> "32"
    | _ -> failwith "Unsupported Architecture"

  let getPerlaEnvVars () =
    let env = Environment.GetEnvironmentVariables()
    let prefix = "PERLA_"

    [ for entry in env do
        let entry = entry :?> DictionaryEntry
        let key = entry.Key :?> string
        let value = entry.Value :?> string

        if key.StartsWith(prefix) then
          (key.Replace(prefix, String.Empty), value) ]

[<RequireQualifiedAccess>]
module Json =
  open System.Text.Json
  open System.Text.Json.Serialization

  let private jsonOptions () =
    let opts = JsonSerializerOptions()
    opts.WriteIndented <- true
    opts.AllowTrailingCommas <- true
    opts.ReadCommentHandling <- JsonCommentHandling.Skip
    opts.UnknownTypeHandling <- JsonUnknownTypeHandling.JsonElement
    opts.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
    opts

  let ToBytes value =
    JsonSerializer.SerializeToUtf8Bytes(value, jsonOptions ())

  let FromBytes<'T> (bytes: byte array) =
    JsonSerializer.Deserialize<'T>(ReadOnlySpan bytes, jsonOptions ())

  let ToText value =
    JsonSerializer.Serialize(value, jsonOptions ())

  let ToTextMinified value =
    let opts = jsonOptions ()
    opts.WriteIndented <- false
    JsonSerializer.Serialize(value, opts)

  let ToPackageJson dependencies =
    JsonSerializer.Serialize({| dependencies = dependencies |}, jsonOptions ())

module Logger =
  open System.Threading.Tasks
  open Spectre.Console

  [<Literal>]
  let private LogPrefix = "Perla:"

  [<Literal>]
  let private ScaffoldPrefix = "Scaffolding:"

  [<Literal>]
  let private BuildPrefix = "Build:"

  [<Literal>]
  let private ServePrefix = "Serve:"

  [<Literal>]
  let private PluginPrefix = "Plugin:"

  [<Struct>]
  type PrefixKind =
    | Log
    | Scaffold
    | Build
    | Serve
    | Plugin

  [<Struct>]
  type LogEnding =
    | NewLine
    | SameLine

  let format (prefix: PrefixKind list) (message: string) : FormattableString =
    let prefix =
      prefix
      |> List.fold
           (fun cur next ->
             let pr =
               match next with
               | PrefixKind.Log -> LogPrefix
               | PrefixKind.Scaffold -> ScaffoldPrefix
               | PrefixKind.Build -> BuildPrefix
               | PrefixKind.Serve -> ServePrefix
               | PrefixKind.Plugin -> PluginPrefix

             $"{cur}{pr}")
           ""

    $"[yellow]{prefix}[/] {message}"

  type Logger =
    static member log(message, ?ex: exn, ?prefixes, ?ending, ?escape) =
      let prefixes =
        let prefixes = defaultArg prefixes [ Log ]

        if prefixes.Length = 0 then [ Log ] else prefixes

      let escape = defaultArg escape true
      let formatted = format prefixes message

      match (defaultArg ending NewLine) with
      | NewLine ->
        if escape then
          AnsiConsole.MarkupLineInterpolated formatted
        else
          AnsiConsole.MarkupLine(formatted.ToString())
      | SameLine ->
        if escape then
          AnsiConsole.MarkupInterpolated formatted
        else
          AnsiConsole.Markup(formatted.ToString())

      match ex with
      | Some ex ->
#if DEBUG
        AnsiConsole.WriteException(
          ex,
          ExceptionFormats.ShortenEverything ||| ExceptionFormats.ShowLinks
        )
#else
        AnsiConsole.WriteException(
          ex,
          ExceptionFormats.ShortenPaths ||| ExceptionFormats.ShowLinks
        )
#endif
      | None -> ()

    static member scaffold(message, ?ex: exn, ?ending, ?escape) =
      Logger.log (
        message,
        ?ex = ex,
        prefixes = [ Log; Scaffold ],
        ?ending = ending,
        ?escape = escape
      )

    static member pluginLog(message, ?ex: exn, ?ending, ?escape) =
      Logger.log (
        message,
        ?ex = ex,
        prefixes = [ Log; Plugin ],
        ?ending = ending,
        ?escape = escape
      )

    static member build(message, ?ex: exn, ?ending, ?escape) =
      Logger.log (
        message,
        ?ex = ex,
        prefixes = [ Log; Build ],
        ?ending = ending,
        ?escape = escape
      )

    static member serve(message, ?ex: exn, ?ending, ?escape) =
      Logger.log (
        message,
        ?ex = ex,
        prefixes = [ Log; Serve ],
        ?ending = ending,
        ?escape = escape
      )

    static member spinner<'Operation>
      (
        title: string,
        task: Task<'Operation>
      ) : Task<'Operation> =
      let status = AnsiConsole.Status()
      status.Spinner <- Spinner.Known.Dots
      status.StartAsync(title, (fun _ -> task))

    static member spinner<'Operation>
      (
        title: string,
        task: Async<'Operation>
      ) : Task<'Operation> =
      let status = AnsiConsole.Status()
      status.Spinner <- Spinner.Known.Dots
      status.StartAsync(title, (fun _ -> task |> Async.StartAsTask))


    static member inline spinner<'Operation>
      (
        title: string,
        [<InlineIfLambda>] operation: StatusContext -> Task<'Operation>
      ) : Task<'Operation> =
      let status = AnsiConsole.Status()
      status.Spinner <- Spinner.Known.Dots
      status.StartAsync(title, operation)

    static member inline spinner<'Operation>
      (
        title: string,
        [<InlineIfLambda>] operation: StatusContext -> Async<'Operation>
      ) : Task<'Operation> =
      let status = AnsiConsole.Status()
      status.Spinner <- Spinner.Known.Dots
      status.StartAsync(title, (fun ctx -> operation ctx |> Async.StartAsTask))

  let getPerlaLogger () =
    { new ILogger with
        member _.Log(logLevel, eventId, state, ex, formatter) =
          let format = formatter.Invoke(state, ex)
          Logger.log (format)

        member _.IsEnabled(level) = true

        member _.BeginScope(state) =
          { new IDisposable with
              member _.Dispose() = () } }

[<RequireQualifiedAccessAttribute>]
module Http =
  open Flurl
  open Flurl.Http
  open Logger

  [<Literal>]
  let SKYPACK_CDN = "https://cdn.skypack.dev"

  [<Literal>]
  let SKYPACK_API = "https://api.skypack.dev/v1"

  [<Literal>]
  let JSPM_API = "https://api.jspm.io/generate"

  let private getSkypackInfo (name: string) (alias: string) =
    taskResult {
      try
        let info = {| lookUp = $"%s{name}" |}
        let! res = $"{SKYPACK_CDN}/{info.lookUp}".GetAsync()

        if res.StatusCode >= 400 then
          return! PackageNotFoundException |> Error

        let mutable pinnedUrl = ""
        let mutable importUrl = ""

        let info =
          if res.Headers.TryGetFirst("x-pinned-url", &pinnedUrl) |> not then
            {| info with pin = None |}
          else
            {| info with pin = Some pinnedUrl |}

        let info =
          if res.Headers.TryGetFirst("x-import-url", &importUrl) |> not then
            {| info with import = None |}
          else
            {| info with import = Some importUrl |}

        return
          [ alias, $"{SKYPACK_CDN}{info.pin |> Option.defaultValue info.lookUp}" ],
          // skypack doesn't handle any import maps so the scopes will always be empty
          []
      with
      | :? Flurl.Http.FlurlHttpException as ex ->
        match ex.StatusCode |> Option.ofNullable with
        | Some code when code >= 400 ->
          return! PackageNotFoundException |> Error
        | _ -> ()

        return! ex :> Exception |> Error
      | ex -> return! ex |> Error
    }

  let private getJspmInfo name alias source =
    taskResult {
      let queryParams =
        {| install = [| $"{name}" |]
           env = "browser"
           provider =
            match source with
            | Source.Skypack -> "skypack"
            | Source.Jspm -> "jspm"
            | Source.Jsdelivr -> "jsdelivr"
            | Source.Unpkg -> "unpkg"
            | _ ->
              Logger.log
                $"Warn: an unknown provider has been specified: [{source}] defaulting to jspm"

              "jspm" |}

      try
        let! res =
          JSPM_API.SetQueryParams(queryParams).GetJsonAsync<JspmResponse>()

        let scopes =
          // F# type serialization hits again!
          // the JSPM response may include a scope object or not
          // so try to safely check if it exists or not
          match res.map.scopes :> obj |> Option.ofObj with
          | None -> Map.empty
          | Some value -> value :?> Map<string, Scope>

        return
          res.map.imports |> Map.toList |> List.map (fun (k, v) -> alias, v),
          scopes |> Map.toList
      with :? Flurl.Http.FlurlHttpException as ex ->
        match ex.StatusCode |> Option.ofNullable with
        | Some code when code >= 400 ->
          return! PackageNotFoundException |> Error
        | _ -> ()

        return! ex :> Exception |> Error
    }

  let getPackageUrlInfo (name: string) (alias: string) (source: Source) =
    match source with
    | Source.Skypack -> getSkypackInfo name alias
    | _ -> getJspmInfo name alias source

  let searchPackage (name: string) (page: int option) =
    taskResult {
      let page = defaultArg page 1

      let! res =
        SKYPACK_API
          .AppendPathSegment("search")
          .SetQueryParams({| q = name; p = page |})
          .GetJsonAsync<SkypackSearchResponse>()

      return
        {| meta = res.meta
           results = res.results |}
    }

  let showPackage name =
    taskResult {
      let! res =
        $"{SKYPACK_API}/package/{name}".GetJsonAsync<SkypackPackageResponse>()

      return
        { name = res.name
          versions = res.versions
          distTags = res.distTags
          maintainers = res.maintainers
          license = res.license
          updatedAt = res.updatedAt
          registry = res.registry
          description = res.description
          isDeprecated = res.isDeprecated
          dependenciesCount = res.dependenciesCount
          links = res.links }
    }
