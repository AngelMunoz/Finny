namespace Perla

open System
open System.Text.Json
open System.Text.Json.Serialization

open LiteDB


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

  let (|Typescript|Javascript|Jsx|Css|Json|Other|) value =
    match value with
    | ".ts"
    | ".tsx" -> Typescript
    | ".js" -> Javascript
    | ".jsx" -> Jsx
    | ".json" -> Json
    | ".css" -> Css
    | _ -> Other value

  [<RequireQualifiedAccess>]
  type LoaderType =
    | Typescript
    | Tsx
    | Jsx

  [<RequireQualifiedAccess>]
  type ReloadKind =
    | FullReload
    | HMR

  [<RequireQualifiedAccess>]
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
          [ "*.js"; "*.css"; "*.ts"; "*.tsx"; "*.jsx"; "*.json" ]
          |> List.toSeq
          |> Some
        directories =
          seq {
            "index.html"
            "./src"
          }
          |> Some }

  type DevServerConfig =
    { autoStart: bool option
      port: int option
      host: string option
      mountDirectories: Map<string, string> option
      watchConfig: WatchConfig option
      liveReload: bool option
      useSSL: bool option
      enableEnv: bool option
      envPath: string option }

    static member DefaultConfig() =
      { autoStart = Some true
        port = Some 7331
        host = None
        mountDirectories = Map.ofList ([ "./src", "/src" ]) |> Some
        watchConfig = WatchConfig.Default() |> Some
        liveReload = Some true
        useSSL = Some false
        enableEnv = Some true
        envPath = Some "/env.js" }

  type CopyPaths =
    { includes: (string seq) option
      excludes: (string seq) option }

  type BuildConfig =
    { esBuildPath: string option
      esbuildVersion: string option
      copyPaths: CopyPaths option
      target: string option
      outDir: string option
      bundle: bool option
      format: string option
      minify: bool option
      jsxFactory: string option
      jsxFragment: string option
      injects: (string seq) option
      externals: (string seq) option
      fileLoaders: Map<string, string> option
      emitEnvFile: bool option }

    static member DefaultExcludes() =
      [ "index.html"
        ".fsproj"
        ".fable"
        "fable_modules"
        "bin"
        "obj"
        ".fs"
        ".js"
        ".css"
        ".ts"
        ".jsx"
        ".tsx"
        ".woff"
        ".woff2" ]

    static member DefaultFileLoaders() =
      [ ".png", "file"; ".woff", "file"; ".woff2", "file"; ".svg", "file" ]
      |> Map.ofList

    static member DefaultConfig() =
      { esBuildPath = None
        esbuildVersion = Some Constants.Esbuild_Version
        copyPaths =
          { includes = None
            excludes = BuildConfig.DefaultExcludes() |> Seq.ofList |> Some }
          |> Some
        target = Some Constants.Esbuild_Target
        outDir = None
        bundle = Some true
        format = Some "esm"
        minify = Some true
        jsxFactory = None
        jsxFragment = None
        injects = None
        externals = None
        fileLoaders = BuildConfig.DefaultFileLoaders() |> Some
        emitEnvFile = Some true }

  [<Struct>]
  type Dependency =
    { name: string
      version: string
      alias: string option }

  type PerlaConfig =
    { ``$schema``: string option
      index: string option
      fable: FableConfig option
      devServer: DevServerConfig option
      build: BuildConfig option
      // Deprecate this in the json schema
      packages: Map<string, string> option
      dependencies: Dependency seq option
      devDependencies: Dependency seq option }

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
        packages = None
        dependencies = None
        devDependencies = None }

  type Source =
    | Skypack = 0
    | Jspm = 1
    | Jsdelivr = 2
    | Unpkg = 3
    | JspmSystem = 4

  type InitKind =
    | Full = 0
    | Simple = 1

  type InitOptions =
    { path: string option
      withFable: bool option
      initKind: InitKind option
      yes: bool option }

  type SearchOptions =
    { package: string option
      page: int option }

  type ShowPackageOptions = { package: string option }

  type AddPackageOptions =
    { package: string option
      alias: string option
      source: Source option }

  type RemovePackageOptions = { package: string option }

  type ListFormat =
    | HumanReadable
    | PackageJson

  type ListPackagesOptions = { format: ListFormat }

  type NameParsingErrors =
    | MissingRepoName
    | WrongGithubFormat

    member this.AsString =
      match this with
      | MissingRepoName -> "The repository name is missing"
      | WrongGithubFormat -> "The repository name is not a valid github name"

  type RepositoryOptions =
    { fullRepositoryName: string
      branch: string }

  type ProjectOptions =
    { projectName: string
      templateName: string }

  type RestoreOptions = { source: Source }


  exception CommandNotParsedException of string
  exception HelpRequestedException
  exception MissingPackageNameException
  exception MissingImportMapPathException
  exception PackageNotFoundException
  exception HeaderNotFoundException of string
  exception FailedToParseNameException of string
