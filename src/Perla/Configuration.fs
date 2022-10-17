module Perla.Configuration

open FSharp.UMX
open Perla.Types
open Perla.Units
open Perla.PackageManager.Types


module Types =

  type DevServerField =
    | Port of int
    | Host of string
    | LiveReload of bool
    | UseSSl of bool

  type FableField =
    | Project of string
    | Extension of string
    | SourceMaps of bool
    | OutDir of bool

  [<RequireQualifiedAccess>]
  type PerlaWritableField =
    | Configuration of RunConfiguration
    | Provider of Provider
    | Dependencies of Dependency seq
    | DevDependencies of Dependency seq
    | Fable of FableField seq

open Types
open System.Text.Json.Nodes

module Defaults =

  let FableConfig: FableConfig =
    { project = UMX.tag "./src/App.fsproj"
      extension = UMX.tag ".fs.js"
      sourceMaps = true
      outDir = None }

  let DevServerConfig: DevServerConfig =
    { port = 7331
      host = "127.0.0.1"
      liveReload = true
      useSSL = true
      proxy = Map.empty }

  let EsbuildConfig: EsbuildConfig =
    { esBuildPath = FileSystem.FileSystem.EsbuildBinaryPath()
      version = UMX.tag Constants.Esbuild_Version
      ecmaVersion = Constants.Esbuild_Target
      minify = true
      injects = Seq.empty
      externals = Seq.empty
      fileLoaders =
        [ ".png", "file"; ".woff", "file"; ".woff2", "file"; ".svg", "file" ]
        |> Map.ofList
      jsxFactory = None
      jsxFragment = None }

  let BuildConfig =
    { includes = Seq.empty
      excludes =
        seq {
          "./**/obj/**"
          "./**/bin/**"
          "./**/*.fs"
          "./**/*.fsproj"
        }
      outDir = UMX.tag "./dist"
      emitEnvFile = true }

  let PerlaConfig =
    { index = UMX.tag Constants.IndexFile
      runConfiguration = RunConfiguration.Development
      provider = Provider.Jspm
      build = BuildConfig
      devServer = DevServerConfig
      esbuild = EsbuildConfig
      fable = None
      mountDirectories =
        Map.ofList [ UMX.tag<ServerUrl> "/src", UMX.tag<UserPath> "./src" ]
      enableEnv = true
      envPath = UMX.tag Constants.EnvPath
      dependencies = Seq.empty
      devDependencies = Seq.empty }


module Json =
  open Perla.FileSystem
  open Perla.Json

  let getConfigDocument () : JsonNode option =
    match FileSystem.PerlaConfigText() with
    | Some content ->
      JsonNode.Parse(
        content,
        nodeOptions = Json.DefaultJsonNodeOptions(),
        documentOptions = Json.DefaultJsonDocumentOptions()
      )
      |> Some
    | None -> None

  let updateFileFields
    (jsonContents: byref<JsonNode option>)
    (fields: PerlaWritableField seq)
    : unit =
    let configuration =
      fields
      |> Seq.tryPick (fun f ->
        match f with
        | PerlaWritableField.Configuration config -> Some config
        | _ -> None)
      |> Option.map (fun f -> f.AsString)

    let provider =
      fields
      |> Seq.tryPick (fun f ->
        match f with
        | PerlaWritableField.Provider config -> Some config
        | _ -> None)
      |> Option.map (fun f -> f.AsString)

    let dependencies =
      fields
      |> Seq.tryPick (fun f ->
        match f with
        | PerlaWritableField.Dependencies deps -> Some deps
        | _ -> None)

    let devDependencies =
      fields
      |> Seq.tryPick (fun f ->
        match f with
        | PerlaWritableField.DevDependencies deps -> Some deps
        | _ -> None)

    let fable =
      fields
      |> Seq.tryPick (fun f ->
        match f with
        | PerlaWritableField.Fable fields ->
          let mutable f =
            {| project = None
               extension = None
               sourceMaps = None
               outDir = None |}

          for field in fields do
            match field with
            | FableField.Project path -> f <- {| f with project = Some path |}
            | FableField.Extension ext -> f <- {| f with extension = Some ext |}
            | FableField.SourceMaps sourceMaps ->
              f <-
                {| f with
                     sourceMaps = Some sourceMaps |}
            | FableField.OutDir outDir -> f <- {| f with outDir = Some outDir |}

          Some f
        | _ -> None)

    let addConfig (content: JsonNode) =
      match configuration with
      | Some config -> content["runConfiguration"] <- Json.ToNode(config)
      | None -> ()

      content

    let addProvider (content: JsonNode) =
      match provider with
      | Some config -> content["provider"] <- Json.ToNode(config)
      | None -> ()

      content

    let addDeps (content: JsonNode) =
      match dependencies with
      | Some deps -> content["dependencies"] <- Json.ToNode(deps)
      | None -> ()

      content

    let addDevDeps (content: JsonNode) =
      match devDependencies with
      | Some deps -> content["devDependencies"] <- Json.ToNode(deps)
      | None -> ()

      content

    let addFable (content: JsonNode) =
      match fable with
      | Some fable -> content["fable"] <- Json.ToNode(fable)
      | None -> ()

      content

    let content =
      match jsonContents with
      | Some content -> content
      | None ->
        JsonNode.Parse($"""{{ "$schema": "{Constants.JsonSchemaUrl}" }}""")

    match content[ "$schema" ].GetValue<string>() |> Option.ofObj with
    | Some _ -> ()
    | None -> content["$schema"] <- Json.ToNode(Constants.JsonSchemaUrl)

    jsonContents <-
      content
      |> addConfig
      |> addProvider
      |> addDeps
      |> addDevDeps
      |> addFable
      |> Some

  let fromFable (jsonNode: JsonNode) (config: PerlaConfig) =
    match jsonNode[ "fable" ].AsObject() |> Option.ofObj, config.fable with
    | None, None
    | None, _ -> jsonNode, config
    | Some content, fable ->
      let fable = defaultArg fable Defaults.FableConfig

      let project =
        match content.TryGetPropertyValue("project") with
        | true, value -> value.GetValue()
        | false, _ -> UMX.untag fable.project

      let extension =
        match content.TryGetPropertyValue("extension") with
        | true, value -> value.GetValue()
        | false, _ -> UMX.untag fable.extension

      let sourceMaps =
        match content.TryGetPropertyValue("sourceMaps") with
        | true, value -> value.GetValue()
        | false, _ -> fable.sourceMaps

      let outDir =
        match content.TryGetPropertyValue("outDir") with
        | true, value -> value.GetValue()
        | false, _ -> fable.outDir |> Option.map UMX.untag
        |> Option.map UMX.tag

      let fable =
        { fable with
            project = UMX.tag project
            extension = UMX.tag extension
            sourceMaps = sourceMaps
            outDir = outDir }

      jsonNode, { config with fable = Some fable }

  let fromDevServer (jsonNode: JsonNode) (config: PerlaConfig) =
    match jsonNode[ "devServer" ].AsObject() |> Option.ofObj with
    | None -> jsonNode, config
    | Some jsonContent ->
      let content =
        jsonContent.GetValue<{| port: int option
                                host: string option
                                liveReload: bool option
                                useSSL: bool option |}>
          ()

      let devServer =
        let devServer = config.devServer

        { devServer with
            port = defaultArg content.port devServer.port
            host = defaultArg content.host devServer.host
            liveReload = defaultArg content.liveReload devServer.liveReload
            useSSL = defaultArg content.useSSL devServer.useSSL }

      jsonNode, { config with devServer = devServer }

  let fromEsbuildConfig (jsonNode: JsonNode) (config: PerlaConfig) =
    match jsonNode[ "esbuild" ].AsObject() |> Option.ofObj with
    | None -> jsonNode, config
    | Some content ->
      let content =
        content.GetValue<{| esBuildPath: string option
                            version: string option
                            includes: string seq option
                            excludes: string seq option
                            ecmaVersion: string option
                            outDir: string option
                            minify: bool option
                            injects: string seq option
                            externals: string seq option
                            fileLoaders: Map<string, string> option
                            emitEnvFile: bool option
                            jsxFactory: string option
                            jsxFragment: string option |}>
          ()

      let esbuild =
        let esbuild = config.esbuild

        { esbuild with
            esBuildPath =
              (defaultArg content.esBuildPath (UMX.untag esbuild.esBuildPath))
              |> UMX.tag
            version =
              (defaultArg content.version (UMX.untag esbuild.version))
              |> UMX.tag
            ecmaVersion =
              (defaultArg content.ecmaVersion (UMX.untag esbuild.ecmaVersion))
              |> UMX.tag
            minify =
              (defaultArg content.minify (UMX.untag esbuild.minify)) |> UMX.tag
            injects = (defaultArg content.injects esbuild.injects)
            externals = (defaultArg content.externals esbuild.externals)
            fileLoaders = defaultArg content.fileLoaders esbuild.fileLoaders
            jsxFactory = content.jsxFactory |> Option.orElse esbuild.jsxFactory
            jsxFragment =
              content.jsxFragment |> Option.orElse esbuild.jsxFragment }

      jsonNode, { config with esbuild = esbuild }

  let fromBuildConfig (jsonNode: JsonNode) (config: PerlaConfig) =
    match jsonNode[ "build" ].AsObject() |> Option.ofObj with
    | None -> jsonNode, config
    | Some content ->
      let content =
        content.GetValue<{| includes: string seq option
                            excludes: string seq option
                            outDir: string option
                            emitEnvFile: bool option |}>
          ()

      let build =
        let build = config.build

        { build with
            includes = defaultArg content.includes build.includes
            excludes = defaultArg content.excludes build.excludes
            outDir =
              (defaultArg content.outDir (UMX.untag build.outDir)) |> UMX.tag
            emitEnvFile = defaultArg content.emitEnvFile build.emitEnvFile }

      jsonNode, { config with build = build }

  let fromPerla (jsonNode: JsonNode) (config: PerlaConfig) =
    match jsonNode.AsObject() |> Option.ofObj with
    | None -> config
    | Some root ->
      let index =
        match root.TryGetPropertyValue("index") with
        | true, value -> value.GetValue()
        | false, _ -> UMX.untag config.index

      let provider =
        match root.TryGetPropertyValue("provider") with
        | true, value -> value.GetValue()
        | false, _ -> config.provider.AsString

      let runConfiguration =
        match root.TryGetPropertyValue("runConfiguration") with
        | true, value -> value.GetValue()
        | false, _ -> config.runConfiguration.AsString

      let mountDirectories =
        match root.TryGetPropertyValue("mountDirectories") with
        | true, value -> value.GetValue()
        | false, _ -> config.mountDirectories

      let enableEnv =
        match root.TryGetPropertyValue("enableEnv") with
        | true, value -> value.GetValue()
        | false, _ -> config.enableEnv

      let envPath =
        match root.TryGetPropertyValue("envPath") with
        | true, value -> value.GetValue()
        | false, _ -> config.envPath

      let dependencies =
        match root.TryGetPropertyValue("dependencies") with
        | true, value -> value.GetValue()
        | false, _ -> config.dependencies

      let devDependencies =
        match root.TryGetPropertyValue("devDependencies") with
        | true, value -> value.GetValue()
        | false, _ -> config.devDependencies

      { config with
          index = UMX.tag index
          provider = provider |> Provider.FromString
          runConfiguration = runConfiguration |> RunConfiguration.FromString
          mountDirectories = mountDirectories
          enableEnv = enableEnv
          envPath = envPath
          dependencies = dependencies
          devDependencies = devDependencies }

// will enable in the future
let FromEnv (config: PerlaConfig) : PerlaConfig = config

let FromCli
  (runConfig: RunConfiguration option)
  (provider: Provider option)
  (serverOptions: DevServerField seq option)
  (config: PerlaConfig)
  : PerlaConfig =
  let configuration = defaultArg runConfig Defaults.PerlaConfig.runConfiguration
  let provider = defaultArg provider Defaults.PerlaConfig.provider

  let serverOptions = defaultArg serverOptions []

  let defaults =
    Defaults.DevServerConfig.port,
    Defaults.DevServerConfig.host,
    Defaults.DevServerConfig.liveReload,
    Defaults.DevServerConfig.useSSL

  let (port, host, liveReload, useSSL) =
    serverOptions
    |> Seq.fold
         (fun (port, host, liveReload, useSSL) next ->
           match next with
           | Port port -> port, host, liveReload, useSSL
           | Host host -> port, host, liveReload, useSSL
           | LiveReload liveReload -> port, host, liveReload, useSSL
           | UseSSl useSSL -> port, host, liveReload, useSSL)
         defaults

  { config with
      devServer =
        { config.devServer with
            port = port
            host = host
            liveReload = liveReload
            useSSL = useSSL }
      runConfiguration = configuration
      provider = provider }

let FromFile (fileContent: JsonNode option) (config: PerlaConfig) =
  match fileContent with
  | Some fileContent ->
    (fileContent, config)
    ||> Json.fromFable
    ||> Json.fromDevServer
    ||> Json.fromBuildConfig
    ||> Json.fromEsbuildConfig
    ||> Json.fromPerla
  | None -> config

/// <summary>
/// </summary>
type Configuration() =

  let mutable _runConfig = None
  let mutable _provider = None
  let mutable _serverOptions = None
  let mutable _fileConfig = Json.getConfigDocument ()

  let mutable _configContents =
    Defaults.PerlaConfig
    |> FromEnv
    |> FromFile _fileConfig
    |> FromCli _runConfig _provider _serverOptions


  let runPipeline () =
    _configContents <-
      Defaults.PerlaConfig
      |> FromEnv
      |> FromFile _fileConfig
      |> FromCli _runConfig _provider _serverOptions

  member val CurrentConfig = _configContents

  member _.UpdateFromCliArgs
    (
      ?runConfig: RunConfiguration,
      ?serverOptions: DevServerField seq
    ) =
    _runConfig <- runConfig
    _serverOptions <- serverOptions

    runPipeline ()

  member _.UpdateFromFile() =
    _fileConfig <- Json.getConfigDocument ()
    runPipeline ()

  member _.WriteFieldsToFile(newValues: PerlaWritableField seq) =
    Json.updateFileFields &_fileConfig newValues
    runPipeline ()

let Configuration = Configuration()
