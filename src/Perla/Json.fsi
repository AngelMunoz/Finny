namespace Perla.Json

open System
open System.Text.Json
open System.Text.Json.Nodes
open Perla.PackageManager.Types
open Perla.Types
open Perla.Units
open Thoth.Json.Net

open FSharp.UMX

module TemplateDecoders =
  type DecodedTemplateConfigItem = {
    id: string
    name: string
    path: string<SystemPath>
    shortName: string
    description: string option
  }

  type DecodedTemplateConfiguration = {
    name: string
    group: string
    templates: DecodedTemplateConfigItem seq
    author: string option
    license: string option
    description: string option
    repositoryUrl: string option
  }

  val TemplateConfigItemDecoder: Decoder<DecodedTemplateConfigItem>

  val TemplateConfigurationDecoder: Decoder<DecodedTemplateConfiguration>

module ConfigDecoders =


  type DecodedFableConfig = {
    project: string<SystemPath> option
    extension: string<FileExtension> option
    sourceMaps: bool option
    outDir: string<SystemPath> option
  }

  type DecodedDevServer = {
    port: int option
    host: string option
    liveReload: bool option
    useSSL: bool option
    proxy: Map<string, string> option
  }

  type DecodedEsbuild = {
    esBuildPath: string<SystemPath> option
    version: string<Semver> option
    ecmaVersion: string option
    minify: bool option
    injects: string seq option
    externals: string seq option
    fileLoaders: Map<string, string> option
    jsxAutomatic: bool option
    jsxImportSource: string option
  }

  type DecodedBuild = {
    includes: string seq option
    excludes: string seq option
    outDir: string<SystemPath> option
    emitEnvFile: bool option
  }

  type DecodedTesting = {
    browsers: Browser seq option
    includes: string seq option
    excludes: string seq option
    watch: bool option
    headless: bool option
    browserMode: BrowserMode option
    fable: DecodedFableConfig option
  }

  type DecodedPerlaConfig = {
    index: string<SystemPath> option
    runConfiguration: RunConfiguration option
    provider: Provider option
    plugins: string list option
    build: DecodedBuild option
    devServer: DecodedDevServer option
    fable: DecodedFableConfig option
    esbuild: DecodedEsbuild option
    testing: DecodedTesting option
    mountDirectories: Map<string<ServerUrl>, string<UserPath>> option
    enableEnv: bool option
    envPath: string<ServerUrl> option
    paths: Map<string<BareImport>, string<ResolutionUrl>> option
    dependencies: Dependency seq option
    devDependencies: Dependency seq option
  }

  val PerlaDecoder: Decoder<DecodedPerlaConfig>

[<RequireQualifiedAccess>]
module internal TestDecoders =
  val TestStats: Decoder<TestStats>
  val Test: Decoder<Test>
  val Suite: Decoder<Suite>

[<RequireQualifiedAccess>]
module internal EventDecoders =
  val SessionStart: Decoder<Guid * TestStats * int>
  val SessionEnd: Decoder<Guid * TestStats>
  val SuiteEvent: Decoder<Guid * TestStats * Suite>
  val TestPass: Decoder<Guid * TestStats * Test>
  val TestFailed: Decoder<Guid * TestStats * Test * string * string>
  val ImportFailed: Decoder<Guid * string * string>

[<RequireQualifiedAccess>]
module internal ConfigEncoders =
  val Browser: Encoder<Browser>
  val BrowserMode: Encoder<BrowserMode>
  val TestConfig: Encoder<TestConfig>

open ConfigDecoders

[<RequireQualifiedAccess; Struct>]
type PerlaConfigSection =
  | Index of index: string option
  | Fable of fable: FableConfig option
  | DevServer of devServer: DevServerConfig option
  | Build of build: BuildConfig option
  | Dependencies of dependencies: Dependency seq option
  | DevDependencies of devDependencies: Dependency seq option

[<RequireQualifiedAccess>]
module Json =

  val inline ToBytes<'Value, 'Serializer
    when 'Serializer: (static member SerializeToUtf8Bytes:
      'Value * options: JsonSerializerOptions -> byte array)> :
    'Value -> byte array

  val inline FromBytes<'Value, 'Serializer
    when 'Serializer: (static member Deserialize:
      byte ReadOnlySpan * JsonSerializerOptions -> 'Value)> :
    byte array -> 'Value

  val inline ToText<'Value, 'Serializer
    when 'Serializer: (static member Serialize:
      'Value * JsonSerializerOptions -> string)> :

    minify: bool -> value: 'Value -> string

  val inline ToNode<'Value, 'Serializer
    when 'Serializer: (static member SerializeToNode:
      'Value * JsonSerializerOptions -> JsonNode)> : value: 'Value -> JsonNode

  val inline GetConfigDocument<'JsonObject
    when 'JsonObject: (static member Parse:
      string * Nullable<JsonNodeOptions> * JsonDocumentOptions -> JsonNode)> :
    jsonText: string -> JsonObject

  val inline FromConfigFile:
    jsonString: string -> Result<ConfigDecoders.DecodedPerlaConfig, string>

  val TestEventFromJson: jsonString: string -> Result<TestEvent, string>
