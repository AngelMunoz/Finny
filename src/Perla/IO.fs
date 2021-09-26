namespace Perla

open System
open FsToolkit.ErrorHandling
open Types

[<RequireQualifiedAccess>]
module Env =
  open System.IO
  open System.Runtime.InteropServices

  [<Literal>]
  let FdsDirectoryName = ".fsdevserver"

  let getToolsPath () =
    let user =
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)

    Path.Combine(user, FdsDirectoryName)

  let isWindows =
    RuntimeInformation.IsOSPlatform(OSPlatform.Windows)

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

[<RequireQualifiedAccess>]
module internal Json =
  open System.Text.Json
  open System.Text.Json.Serialization

  let private jsonOptions () =
    let opts = JsonSerializerOptions()
    opts.WriteIndented <- true
    opts.AllowTrailingCommas <- true
    opts.ReadCommentHandling <- JsonCommentHandling.Skip
#if NET6_0
    opts.UnknownTypeHandling <- JsonUnknownTypeHandling.JsonElement
    opts.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
#endif

#if NET5_0
    opts.IgnoreNullValues <- true
#endif
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

[<RequireQualifiedAccessAttribute>]
module internal Http =
  open Flurl
  open Flurl.Http

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
          if
            res.Headers.TryGetFirst("x-pinned-url", &pinnedUrl)
            |> not
          then
            {| info with pin = None |}
          else
            {| info with pin = Some pinnedUrl |}

        let info =
          if
            res.Headers.TryGetFirst("x-import-url", &importUrl)
            |> not
          then
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

  let getJspmInfo name alias source =
    taskResult {
      let queryParams =
        {| install = [| $"npm:{name}" |]
           env = "browser"
           provider =
             match source with
             | Source.Skypack -> "skypack"
             | Source.Jspm -> "jspm"
             | Source.Jsdelivr -> "jsdelivr"
             | Source.Unpkg -> "unpkg"
             | _ ->
               printfn
                 $"Warn: An unknown provider has been specied: [{source}] defaulting to jspm"

               "jspm" |}

      try
        let! res =
          JSPM_API
            .SetQueryParams(queryParams)
            .GetJsonAsync<JspmResponse>()

        let scopes =
          // F# type serialization hits again!
          // the JSPM response may include a scope object or not
          // so try to safely check if it exists or not
          match res.map.scopes :> obj |> Option.ofObj with
          | None -> Map.empty
          | Some value -> value :?> Map<string, Scope>

        return
          res.map.imports
          |> Map.toList
          |> List.map (fun (k, v) -> alias, v),
          scopes |> Map.toList
      with
      | :? Flurl.Http.FlurlHttpException as ex ->
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
        $"{SKYPACK_API}/package/{name}"
          .GetJsonAsync<SkypackPackageResponse>()

      return
        {| name = res.name
           versions = res.versions
           distTags = res.distTags
           maintainers = res.maintainers
           license = res.license
           updatedAt = res.updatedAt
           registry = res.registry
           description = res.description
           isDeprecated = res.isDeprecated
           dependenciesCount = res.dependenciesCount
           links = res.links |}
    }

[<RequireQualifiedAccess>]
module Fs =
  open System.IO
  open FSharp.Control.Reactive

  [<Literal>]
  let FdsConfigName = "perla.jsonc"

  type IFileWatcher =
    inherit IDisposable
    abstract member FileChanged : IObservable<string>

  type Paths() =
    static member GetFdsConfigPath(?path: string) =
      $"{defaultArg path (Environment.CurrentDirectory)}/{FdsConfigName}"

  let getFdsConfig filepath =
    try
      let bytes = File.ReadAllBytes filepath
      Json.FromBytes<FdsConfig> bytes |> Ok
    with
    | ex -> ex |> Error

  let ensureParentDirectory path =
    try
      Directory.CreateDirectory(path) |> ignore |> Ok
    with
    | ex -> ex |> Error

  let createFdsConfig path config =
    let serialized = Json.ToBytes config

    try
      File.WriteAllBytes(path, serialized) |> Ok
    with
    | ex -> Error ex

  let getorCreateLockFile configPath =
    taskResult {
      try
        let path = Path.GetFullPath($"%s{configPath}.lock")

        do! ensureParentDirectory (Path.GetDirectoryName(path))

        let bytes = File.ReadAllBytes(path)

        return Json.FromBytes<PackagesLock> bytes
      with
      | :? System.IO.FileNotFoundException ->
        return
          { imports = Map.empty
            scopes = Map.empty }
      | ex -> return! ex |> Error
    }

  let writeLockFile configPath (fdsLock: PackagesLock) =
    let path = Path.GetFullPath($"%s{configPath}.lock")
    let serialized = Json.ToBytes fdsLock

    try
      File.WriteAllBytes(path, serialized) |> Ok
    with
    | ex -> Error ex



  let getOrCreateImportMap path =
    taskResult {
      try
        let path = Path.GetFullPath(path)

        do! ensureParentDirectory (Path.GetDirectoryName(path))

        let bytes = File.ReadAllBytes(path)

        return Json.FromBytes<ImportMap> bytes
      with
      | :? System.IO.FileNotFoundException ->
        return
          { imports = Map.empty
            scopes = Map.empty }
      | ex -> return! ex |> Error
    }

  let writeImportMap path importMap =
    result {
      let bytes = Json.ToBytes importMap

      try
        let path = Path.GetFullPath(path)

        Directory.CreateDirectory(Path.GetDirectoryName(path))
        |> ignore

        File.WriteAllBytes(path, bytes)
      with
      | ex -> return! ex |> Error
    }

  let getFileWatcher (config: WatchConfig) =
    let watchers =
      (defaultArg config.directories ([ "./src" ] |> Seq.ofList))
      |> Seq.map
           (fun dir ->
             let fsw = new FileSystemWatcher(dir)
             fsw.IncludeSubdirectories <- true
             fsw.NotifyFilter <- NotifyFilters.FileName ||| NotifyFilters.Size

             let filters =
               defaultArg config.extensions (Seq.ofList [ "*.js"; "*.css" ])

             for filter in filters do
               fsw.Filters.Add(filter)

             fsw.EnableRaisingEvents <- true
             fsw)

    let subs =
      watchers
      |> Seq.map
           (fun watcher ->
             [ watcher.Renamed
               |> Observable.map (fun e -> e.Name)
               watcher.Changed
               |> Observable.map (fun e -> e.Name)
               watcher.Deleted
               |> Observable.map (fun e -> e.Name) ]
             |> Observable.mergeSeq)

    { new IFileWatcher with
        override _.Dispose() : unit =
          watchers
          |> Seq.iter (fun watcher -> watcher.Dispose())

        override _.FileChanged: IObservable<string> = Observable.mergeSeq subs }
