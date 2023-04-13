module Perla.Configuration

open FSharp.UMX
open Perla.Types
open Perla.Units
open Perla.PackageManager.Types
open Perla.Logger
open System.Runtime.InteropServices

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Types =

  type DevServerField =
    | Port of int
    | Host of string
    | LiveReload of bool
    | UseSSL of bool
    | MinifySources of bool

  [<RequireQualifiedAccess>]
  type TestingField =
    | Browsers of Browser seq
    | Includes of string seq
    | Excludes of string seq
    | Watch of bool
    | Headless of bool
    | BrowserMode of BrowserMode

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

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Defaults =

  let FableConfig: FableConfig = {
    project = UMX.tag "./src/App.fsproj"
    extension = UMX.tag ".fs.js"
    sourceMaps = true
    outDir = None
  }

  let DevServerConfig: DevServerConfig = {
    port = 7331
    host = "localhost"
    liveReload = true
    useSSL = false
    proxy = Map.empty
  }

  let EsbuildConfig: EsbuildConfig = {
    esBuildPath = FileSystem.FileSystem.EsbuildBinaryPath None
    version = UMX.tag Constants.Esbuild_Version
    ecmaVersion = Constants.Esbuild_Target
    minify = true
    injects = Seq.empty
    externals = Seq.empty
    fileLoaders =
      [ ".png", "file"; ".woff", "file"; ".woff2", "file"; ".svg", "file" ]
      |> Map.ofList
    jsxAutomatic = false
    jsxImportSource = None
  }

  let BuildConfig = {
    includes = Seq.empty
    excludes = seq {
      "./**/obj/**"
      "./**/bin/**"
      "./**/*.fs"
      "./**/*.fsproj"
    }
    outDir = UMX.tag "./dist"
    emitEnvFile = true
  }

  let TestFableConfig = {
    project = UMX.tag "./tests/App.Tests.fsproj"
    extension = UMX.tag ".fs.js"
    sourceMaps = true
    outDir = None
  }

  let TestConfig = {
    browsers = [ Browser.Chromium ]
    includes = [
      "**/*.test.js"
      "**/*.spec.js"
      "**/*.Test.fs.js"
      "**/*.Spec.fs.js"
    ]
    excludes = []
    watch = false
    headless = true
    browserMode = BrowserMode.Parallel
    fable = None
  }

  let PerlaConfig = {
    index = UMX.tag Constants.IndexFile
    runConfiguration = RunConfiguration.Production
    provider = Provider.Jspm
    plugins = [ "perla-esbuild-plugin" ]
    build = BuildConfig
    devServer = DevServerConfig
    esbuild = EsbuildConfig
    testing = TestConfig
    fable = None
    mountDirectories =
      Map.ofList [ UMX.tag<ServerUrl> "/src", UMX.tag<UserPath> "./src" ]
    enableEnv = true
    envPath = UMX.tag Constants.EnvPath
    dependencies = Seq.empty
    devDependencies = Seq.empty
  }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal Json =
  open Perla.Json

  let getConfigDocument (perlaJsonText: string) : JsonObject =
    JsonObject
      .Parse(
        perlaJsonText,
        nodeOptions = Json.DefaultJsonNodeOptions(),
        documentOptions = Json.DefaultJsonDocumentOptions()
      )
      .AsObject()

  let updateFileFields
    (jsonContents: byref<JsonObject option>)
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
          let mutable f = {|
            project = None
            extension = None
            sourceMaps = None
            outDir = None
          |}

          for field in fields do
            match field with
            | FableField.Project path -> f <- {| f with project = Some path |}
            | FableField.Extension ext ->
              f <- {| f with extension = Some ext |}
            | FableField.SourceMaps sourceMaps ->
              f <- {|
                f with
                    sourceMaps = Some sourceMaps
              |}
            | FableField.OutDir outDir ->
              f <- {| f with outDir = Some outDir |}

          Some f
        | _ -> None)

    let addConfig (content: JsonObject) =
      match configuration with
      | Some config -> content["runConfiguration"] <- Json.ToNode(config)
      | None -> ()

      content

    let addProvider (content: JsonObject) =
      match provider with
      | Some config -> content["provider"] <- Json.ToNode(config)
      | None -> ()

      content

    let addDeps (content: JsonObject) =
      match dependencies with
      | Some deps -> content["dependencies"] <- Json.ToNode(deps)
      | None -> ()

      content

    let addDevDeps (content: JsonObject) =
      match devDependencies with
      | Some deps -> content["devDependencies"] <- Json.ToNode(deps)
      | None -> ()

      content

    let addFable (content: JsonObject) =
      match fable with
      | Some fable -> content["fable"] <- Json.ToNode(fable)
      | None -> ()

      content

    let content =
      match jsonContents with
      | Some content -> content
      | None ->
        JsonObject
          .Parse($"""{{ "$schema": "{Constants.JsonSchemaUrl}" }}""")
          .AsObject()

    match
      content["$schema"]
      |> Option.ofObj
      |> Option.map (fun schema -> schema.GetValue<string>() |> Option.ofObj)
      |> Option.flatten
    with
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

// will enable in the future
let fromEnv (config: PerlaConfig) : PerlaConfig = config

let fromCli
  (runConfig: RunConfiguration option)
  (provider: Provider option)
  (serverOptions: DevServerField seq option)
  (testingOptions: TestingField seq option)
  (config: PerlaConfig)
  : PerlaConfig =
  let configuration = defaultArg runConfig Defaults.PerlaConfig.runConfiguration
  let provider = defaultArg provider Defaults.PerlaConfig.provider

  let serverOptions =
    match serverOptions |> Option.defaultValue Seq.empty |> Seq.toList with
    | [] -> [
        DevServerField.Port config.devServer.port
        DevServerField.Host config.devServer.host
        DevServerField.LiveReload config.devServer.liveReload
        DevServerField.UseSSL config.devServer.useSSL
      ]
    | other -> other

  let minify =
    serverOptions
    |> Seq.tryPick (fun opt ->
      match opt with
      | MinifySources minify -> Some minify
      | _ -> None)
    |> Option.defaultValue Defaults.EsbuildConfig.minify

  let defaults =
    Defaults.DevServerConfig.port,
    Defaults.DevServerConfig.host,
    Defaults.DevServerConfig.liveReload,
    Defaults.DevServerConfig.useSSL

  let port, host, liveReload, useSSL =
    serverOptions
    |> Seq.fold
      (fun (port, host, liveReload, useSSL) next ->
        match next with
        | Port port -> port, host, liveReload, useSSL
        | Host host -> port, host, liveReload, useSSL
        | LiveReload liveReload -> port, host, liveReload, useSSL
        | UseSSL useSSL -> port, host, liveReload, useSSL
        | _ -> port, host, liveReload, useSSL)
      defaults

  let testing =
    defaultArg testingOptions Seq.empty
    |> Seq.fold
      (fun current next ->
        match next with
        | TestingField.Browsers value -> { current with browsers = value }
        | TestingField.Includes value -> { current with includes = value }
        | TestingField.Excludes value -> { current with excludes = value }
        | TestingField.Watch value -> { current with watch = value }
        | TestingField.Headless value -> { current with headless = value }
        | TestingField.BrowserMode value -> {
            current with
                browserMode = value
          })
      config.testing

  {
    config with
        devServer = {
          config.devServer with
              port = port
              host = host
              liveReload = liveReload
              useSSL = useSSL
        }
        testing = testing
        runConfiguration = configuration
        esbuild = { config.esbuild with minify = minify }
        provider = provider
  }

let fromFile (fileContent: JsonObject option) (config: PerlaConfig) =
  match fileContent with
  | Some fileContent ->
    match fileContent.ToJsonString() |> Json.Json.FromConfigFile with
    | Ok decoded ->
      let fable =
        match config.fable, decoded.fable with
        | Some fable, Some decoded ->
          Some {
            fable with
                project = defaultArg decoded.project fable.project
                extension = defaultArg decoded.extension fable.extension
                sourceMaps = defaultArg decoded.sourceMaps fable.sourceMaps
                outDir = decoded.outDir
          }
        | None, Some decoded ->
          Some {
            Defaults.FableConfig with
                project =
                  defaultArg decoded.project Defaults.FableConfig.project
                extension =
                  defaultArg decoded.extension Defaults.FableConfig.extension
                sourceMaps =
                  defaultArg decoded.sourceMaps Defaults.FableConfig.sourceMaps
                outDir = decoded.outDir
          }
        | _, _ -> config.fable

      let devServer =
        match decoded.devServer with
        | Some decoded -> {
            config.devServer with
                port = defaultArg decoded.port config.devServer.port
                host = defaultArg decoded.host config.devServer.host
                liveReload =
                  defaultArg decoded.liveReload config.devServer.liveReload
                useSSL = defaultArg decoded.useSSL config.devServer.useSSL
                proxy = defaultArg decoded.proxy config.devServer.proxy
          }
        | None -> config.devServer

      let build =
        match decoded.build with
        | Some build -> {
            config.build with
                includes = defaultArg build.includes config.build.includes
                excludes = defaultArg build.excludes config.build.excludes
                outDir = defaultArg build.outDir config.build.outDir
                emitEnvFile =
                  defaultArg build.emitEnvFile config.build.emitEnvFile
          }
        | None -> config.build

      let esbuild =
        match decoded.esbuild with
        | Some esbuild -> {
            config.esbuild with
                esBuildPath =
                  defaultArg esbuild.esBuildPath config.esbuild.esBuildPath
                version = defaultArg esbuild.version config.esbuild.version
                ecmaVersion =
                  defaultArg esbuild.ecmaVersion config.esbuild.ecmaVersion
                minify = defaultArg esbuild.minify config.esbuild.minify
                injects = defaultArg esbuild.injects config.esbuild.injects
                externals =
                  defaultArg esbuild.externals config.esbuild.externals
                fileLoaders =
                  defaultArg esbuild.fileLoaders config.esbuild.fileLoaders
                jsxAutomatic =
                  defaultArg esbuild.jsxAutomatic config.esbuild.jsxAutomatic
                jsxImportSource = esbuild.jsxImportSource
          }
        | None -> config.esbuild

      let testing =
        let getFable
          (testingFable: Json.ConfigDecoders.DecodedFableConfig option)
          =
          match testingFable with
          | Some testingFable ->
            {
              Defaults.TestFableConfig with
                  project =
                    defaultArg
                      testingFable.project
                      Defaults.TestFableConfig.project
                  extension =
                    defaultArg
                      testingFable.extension
                      Defaults.TestFableConfig.extension
                  sourceMaps =
                    defaultArg
                      testingFable.sourceMaps
                      Defaults.TestFableConfig.sourceMaps
                  outDir = testingFable.outDir
            }
            |> Some
          | None -> None

        match decoded.testing with
        | Some testing -> {
            config.testing with
                browsers = defaultArg testing.browsers config.testing.browsers
                includes = defaultArg testing.includes config.testing.includes
                excludes = defaultArg testing.excludes config.testing.excludes
                watch = defaultArg testing.watch config.testing.watch
                headless = defaultArg testing.headless config.testing.headless
                browserMode =
                  defaultArg testing.browserMode config.testing.browserMode
                fable = getFable testing.fable
          }
        | None -> config.testing

      let plugins =
        match decoded.plugins with
        | Some [] -> config.plugins
        | None -> config.plugins
        | Some others -> others

      {
        config with
            fable = fable
            devServer = devServer
            build = build
            esbuild = esbuild
            testing = testing
            index = defaultArg decoded.index config.index
            runConfiguration =
              defaultArg decoded.runConfiguration config.runConfiguration
            provider = defaultArg decoded.provider config.provider
            plugins = plugins
            mountDirectories =
              defaultArg decoded.mountDirectories config.mountDirectories
            enableEnv = defaultArg decoded.enableEnv config.enableEnv
            envPath = defaultArg decoded.envPath config.envPath
            dependencies = defaultArg decoded.dependencies config.dependencies
            devDependencies =
              defaultArg decoded.devDependencies config.devDependencies
      }
    | Error err ->
      Logger.log (
        $"[bold red] Filed to deserialize perla.json with error {err}"
      )

      config
  | None -> config

type ConfigurationManager
  (
    readPerlaJsonText: unit -> string option,
    writePerlaJsonText: JsonObject option -> unit
  ) =

  let _getPerlaText () =
    match readPerlaJsonText () with
    | Some text -> text |> Json.getConfigDocument |> Some
    | None -> None

  let mutable _runConfig = None
  let mutable _provider = None
  let mutable _serverOptions = None

  let mutable _testingOptions = None

  let mutable _fileConfig = _getPerlaText ()

  let mutable _configContents =
    Defaults.PerlaConfig
    |> fromEnv
    |> fromFile _fileConfig
    |> fromCli _runConfig _provider _serverOptions _testingOptions


  let runPipeline () =
    _configContents <-
      Defaults.PerlaConfig
      |> fromEnv
      |> fromFile _fileConfig
      |> fromCli _runConfig _provider _serverOptions _testingOptions

  member _.CurrentConfig = _configContents

  member _.UpdateFromCliArgs
    (
      [<Optional>] ?runConfig: RunConfiguration,
      [<Optional>] ?provider: Provider,
      [<Optional>] ?serverOptions: DevServerField seq,
      [<Optional>] ?testingOptions: TestingField seq
    ) =
    _runConfig <- runConfig
    _serverOptions <- serverOptions
    _provider <- provider
    _testingOptions <- testingOptions

    runPipeline ()

  member _.UpdateFromFile() =
    _fileConfig <- _getPerlaText ()
    runPipeline ()

  member _.WriteFieldsToFile(newValues: PerlaWritableField seq) =
    Json.updateFileFields &_fileConfig newValues
    writePerlaJsonText _fileConfig
    runPipeline ()


let ConfigurationManager =
  ConfigurationManager(
    FileSystem.FileSystem.PerlaConfigText,
    (fun content -> FileSystem.FileSystem.WritePerlaConfig(?config = content))
  )
