namespace Perla.Lib

open System
open Perla.Lib.Types

[<AutoOpen>]
module Lib =

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

  type ReloadEvents with

    member this.AsString =
      match this with
      | ReloadEvents.FullReload data -> $"event:reload\ndata:{data}\n\n"
      | ReloadEvents.ReplaceCSS data -> $"event:replace-css\ndata:{data}\n\n"
      | ReloadEvents.CompileError err -> $"event:compile-err\ndata:{err}\n\n"

  type NameParsingErrors with

    member this.AsString =
      match this with
      | MissingRepoName -> "The repository name is missing"
      | WrongGithubFormat -> "The repository name is not a valid github name"

  type FableConfig with

    static member DefaultConfig() =
      { autoStart = Some false
        project = Some Constants.DefaultFableProject
        extension = None
        outDir = None }

  type WatchConfig with

    static member DefaultConfig() =
      { extensions =
          seq {
            "*.js"
            "*.css"
            "*.ts"
            "*.tsx"
            "*.jsx"
            "*.json"
          }
          |> Some
        directories =
          let src, _ = Constants.DefaultMount

          seq {
            Constants.DefaultIndexFile
            src
          }
          |> Some }

  type DevServerConfig with

    static member DefaultConfig() =
      { autoStart = Some true
        port = Some Constants.DefaultPort
        host = None
        mountDirectories = Map.ofArray [| Constants.DefaultMount |] |> Some
        watchConfig = WatchConfig.DefaultConfig() |> Some
        liveReload = Some true
        useSSL = Some false
        enableEnv = Some true
        envPath = Some Constants.DefaultEnvPath }

  type BuildConfig with

    static member DefaultExcludes() =
      seq {
        "index.html"
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
        ".woff2"
      }

    static member DefaultFileLoaders() =
      [ ".png", "file"; ".woff", "file"; ".woff2", "file"; ".svg", "file" ]
      |> Map.ofList

    static member DefaultConfig() =
      { esBuildPath = None
        esbuildVersion = Some Constants.Esbuild_Version
        copyPaths =
          { includes = None
            excludes = BuildConfig.DefaultExcludes() |> Some }
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

  type PerlaConfig with

    static member DefaultConfig(?withFable: bool) =
      let fable =
        match withFable with
        | Some true -> FableConfig.DefaultConfig() |> Some
        | _ -> None

      { ``$schema`` = Some Constants.PerlaJsonSchemaURL
        index = Some Constants.DefaultIndexFile
        fable = fable
        devServer = DevServerConfig.DefaultConfig() |> Some
        build = BuildConfig.DefaultConfig() |> Some
        packages = None }

  type PerlaTemplateRepository with

    static member NewClamRepo
      (path: string)
      (name: string, fullName: string, branch: string)
      =
      { _id = Guid.NewGuid().ToString()
        name = name
        fullName = fullName
        branch = branch
        path = path
        createdAt = DateTime.Now
        updatedAt = Nullable() }
