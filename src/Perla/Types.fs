namespace Perla

open System
open System.Text.Json
open System.Text.Json.Serialization
open System.Collections.Generic

module Types =

  let (|RestartFable|StartFable|StopFable|UnknownFable|) =
    function
    | "restart:fable" -> RestartFable
    | "start:fable" -> StartFable
    | "stop:fable" -> StopFable
    | value -> UnknownFable value

  let (|RestartServer|StartServer|StopServer|Clear|Exit|Unknown|) =
    function
    | "restart" -> RestartServer
    | "start" -> StartServer
    | "stop" -> StopServer
    | "clear"
    | "cls" -> Clear
    | "exit"
    | "stop" -> Exit
    | value -> Unknown value

  type LoaderType =
    | Typescript
    | Tsx
    | Jsx

  type FableConfig =
    { autoStart: bool option
      project: string option
      extension: string option
      outDir: string option }

    static member DefaultConfig() =
      { autoStart = Some false
        project = Some "./src/App.fsproj"
        extension = None
        outDir = None }

  type WatchConfig =
    { extensions: string seq option
      directories: string seq option }

    static member Default() =
      { extensions =
          [ "*.js"
            "*.css"
            "*.ts"
            "*.tsx"
            "*.jsx"
            "*.json" ]
          |> List.toSeq
          |> Some
        directories = [ "./src" ] |> List.toSeq |> Some }

  type DevServerConfig =
    { autoStart: bool option
      port: int option
      host: string option
      mountDirectories: Map<string, string> option
      watchConfig: WatchConfig option
      liveReload: bool option
      useSSL: bool option }

    static member DefaultConfig() =
      { autoStart = Some true
        port = Some 7331
        host = None
        mountDirectories = Map.ofList ([ "./src", "/src" ]) |> Some
        watchConfig = WatchConfig.Default() |> Some
        liveReload = Some true
        useSSL = Some false }

  type BuildConfig =
    { esBuildPath: string option
      esbuildVersion: string option
      target: string option
      outDir: string option
      bundle: bool option
      format: string option
      minify: bool option
      externals: (string seq) option }


    static member DefaultConfig() =
      { esBuildPath = None
        esbuildVersion = Some "0.13.2"
        target = Some "es2017"
        outDir = None
        bundle = Some true
        format = Some "esm"
        minify = Some true
        externals = None }

  type FdsConfig =
    { ``$schema``: string option
      index: string option
      fable: FableConfig option
      devServer: DevServerConfig option
      build: BuildConfig option
      packages: Map<string, string> option }

    static member DefaultConfig(?withFable: bool) =
      let fable =
        match withFable with
        | Some true -> FableConfig.DefaultConfig() |> Some
        | _ -> None

      { ``$schema`` =
          Some
            "https://raw.githubusercontent.com/AngelMunoz/Perla/main/perla.schema.json"
        index = Some "./index.html"
        fable = fable
        devServer = DevServerConfig.DefaultConfig() |> Some
        build = BuildConfig.DefaultConfig() |> Some
        packages = None }


  type Scope = Map<string, string>

  type ImportMap =
    { imports: Map<string, string>
      scopes: Map<string, Scope> }

  type PackagesLock = ImportMap

  type Source =
    | Skypack = 0
    | Jspm = 1
    | Jsdelivr = 2
    | Unpkg = 3

  type SkypackSearchResult =
    { createdAt: DateTime
      description: string
      hasTypes: bool
      isDeprecated: bool
      maintainers: {| name: string; email: string |} seq
      name: string
      popularityScore: float
      updatedAt: DateTime }

  type PackageCheck =
    { title: string
      pass: bool option
      url: string }

  type JspmResponse =
    { staticDeps: string seq
      dynamicDeps: string seq
      map: ImportMap }

  type SkypackSearchResponse =
    { meta: {| page: int
               resultsPerPage: int
               time: int
               totalCount: int64
               totalPages: int |}
      results: SkypackSearchResult seq
      [<JsonExtensionData>]
      extras: Map<string, JsonElement> }

  type SkypackPackageResponse =
    { name: string
      versions: Map<string, DateTime>
      maintainers: {| name: string; email: string |} seq
      license: string
      projectType: string
      distTags: Map<string, string>
      keywords: string seq
      updatedAt: DateTime
      links: Map<string, string> seq
      qualityScore: float
      createdAt: DateTime
      buildStatus: string
      registry: string
      readmeHtml: string
      description: string
      popularityScore: float
      isDeprecated: bool
      dependenciesCount: int
      [<JsonExtensionData>]
      extras: Map<string, JsonElement> }

  type InitOptions =
    { path: string option
      withFable: bool option }

  type SearchOptions =
    { package: string option
      page: int option }

  type ShowPackageOptions = { package: string option }

  type AddPackageOptions =
    { package: string option
      alias: string option
      source: Source option }

  type RemovePackageOptions = { package: string option }

  exception CommandNotParsedException of string
  exception HelpRequestedException
  exception MissingPackageNameException
  exception MissingImportMapPathException
  exception PackageNotFoundException
  exception HeaderNotFoundException of string
