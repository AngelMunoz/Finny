module Perla.Json

open System
open System.Text.Json
open System.Text.Json.Serialization
open System.Text.Json.Nodes

open Perla.PackageManager.Types
open Perla.Types

open Perla.Units
open Thoth.Json.Net

open FSharp.UMX

[<RequireQualifiedAccess; Struct>]
type PerlaConfigSection =
  | Index of index: string option
  | Fable of fable: FableConfig option
  | DevServer of devServer: DevServerConfig option
  | Build of build: BuildConfig option
  | Dependencies of dependencies: Dependency seq option
  | DevDependencies of devDependencies: Dependency seq option

let DefaultJsonOptions () =
  JsonSerializerOptions(
    WriteIndented = true,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    UnknownTypeHandling = JsonUnknownTypeHandling.JsonElement,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
  )

let DefaultJsonNodeOptions () =
  JsonNodeOptions(PropertyNameCaseInsensitive = true)

let DefaultJsonDocumentOptions () =
  JsonDocumentOptions(
    AllowTrailingCommas = true,
    CommentHandling = JsonCommentHandling.Skip
  )

module ConfigDecoders =

  type DecodedFableConfig =
    { project: string<SystemPath> option
      extension: string<FileExtension> option
      sourceMaps: bool option
      outDir: string<SystemPath> option }

  type DecodedDevServer =
    { port: int option
      host: string option
      liveReload: bool option
      useSSL: bool option
      proxy: Map<string, string> option }

  type DecodedEsbuild =
    { esBuildPath: string<SystemPath> option
      version: string<Semver> option
      ecmaVersion: string option
      minify: bool option
      injects: string seq option
      externals: string seq option
      fileLoaders: Map<string, string> option
      jsxFactory: string option
      jsxFragment: string option }
  type DecodedBuild =
    { includes: string seq option
      excludes: string seq option
      outDir: string<SystemPath> option
      emitEnvFile: bool option }

  type DecodedPerlaConfig =
    { index: string<SystemPath> option
      runConfiguration: RunConfiguration option
      provider: Provider option
      build: DecodedBuild option
      devServer: DecodedDevServer option
      fable: DecodedFableConfig option
      esbuild: DecodedEsbuild option
      mountDirectories: Map<string<ServerUrl>, string<UserPath>> option
      enableEnv: bool option
      envPath: string<ServerUrl> option
      dependencies: Dependency seq option
      devDependencies: Dependency seq option }

  let FableFileDecoder: Decoder<DecodedFableConfig> =
    Decode.object (fun get ->
      { project = get.Optional.Field "project" Decode.string |> Option.map UMX.tag<SystemPath>
        extension = get.Optional.Field "extension" Decode.string |> Option.map UMX.tag<FileExtension>
        sourceMaps = get.Optional.Field "sourceMaps" Decode.bool
        outDir = get.Optional.Field "outDir" Decode.string |> Option.map UMX.tag<SystemPath> })

  let DevServerDecoder: Decoder<DecodedDevServer> =
    Decode.object (fun get ->
      { port = get.Optional.Field "port" Decode.int
        host = get.Optional.Field "host" Decode.string
        liveReload = get.Optional.Field "liveReload" Decode.bool
        useSSL = get.Optional.Field "useSSL" Decode.bool
        proxy = get.Optional.Field "proxy" (Decode.dict Decode.string) })

  let EsbuildDecoder: Decoder<DecodedEsbuild> =
    Decode.object (fun get ->
      { fileLoaders = get.Optional.Field "fileLoaders" (Decode.dict Decode.string)
        esBuildPath = get.Optional.Field "esBuildPath" Decode.string |> Option.map UMX.tag<SystemPath>
        version = get.Optional.Field "version" Decode.string |> Option.map UMX.tag<Semver>
        ecmaVersion = get.Optional.Field "ecmaVersion" Decode.string
        minify = get.Optional.Field "minify" Decode.bool
        injects = get.Optional.Field "injects" (Decode.list Decode.string) |> Option.map List.toSeq
        externals = get.Optional.Field "externals" (Decode.list Decode.string) |> Option.map List.toSeq
        jsxFactory = get.Optional.Field "jsxFactory" Decode.string
        jsxFragment = get.Optional.Field "jsxFragment" Decode.string })

  let BuildDecoder: Decoder<DecodedBuild> =
    Decode.object (fun get ->
      { includes = get.Optional.Field "includes" (Decode.list Decode.string) |> Option.map List.toSeq
        excludes = get.Optional.Field "excludes" (Decode.list Decode.string) |> Option.map List.toSeq
        outDir = get.Optional.Field "outDir" Decode.string |> Option.map UMX.tag<SystemPath>
        emitEnvFile = get.Optional.Field "emitEnvFile" Decode.bool })

  let DependencyDecoder: Decoder<Dependency> =
    Decode.object (fun get ->
      { name = get.Required.Field "name" Decode.string
        version = get.Required.Field "version" Decode.string
        alias = get.Optional.Field "alias" Decode.string })

  let PerlaDecoder: Decoder<DecodedPerlaConfig> =
    Decode.object (fun get ->
      let runConfigDecoder =
          Decode.string
          |> Decode.andThen(
            function
            | "dev"
            | "development" ->
              Decode.succeed (RunConfiguration.Development)
            | "prod"
            | "production" ->
              Decode.succeed (RunConfiguration.Production)
            | value -> Decode.fail $"{value} is not a valid run configuration")
      let providerDecoder =
          Decode.string
          |> Decode.andThen(
            function
            | "jspm" -> Decode.succeed Provider.Jspm
            | "skypack" -> Decode.succeed Provider.Skypack
            | "unpkg" -> Decode.succeed Provider.Unpkg
            | "jsdelivr" -> Decode.succeed Provider.Jsdelivr
            | "jspm.system" -> Decode.succeed Provider.JspmSystem
            | value -> Decode.fail $"{value} is not a valid run configuration")

      { index = get.Optional.Field "index" Decode.string |> Option.map UMX.tag<SystemPath>
        runConfiguration = get.Optional.Field "runConfiguration" runConfigDecoder
        provider = get.Optional.Field "provider" providerDecoder
        build = get.Optional.Field "build" BuildDecoder
        devServer = get.Optional.Field "devServer" DevServerDecoder
        fable = get.Optional.Field "fable" FableFileDecoder
        esbuild = get.Optional.Field "esbuild" EsbuildDecoder
        mountDirectories = get.Optional.Field "mountDirectories" (Decode.dict Decode.string) |> Option.map(fun m -> m |> Map.toSeq |> Seq.map (fun (k, v) -> UMX.tag<ServerUrl> k , UMX.tag<UserPath> v) |> Map.ofSeq)
        enableEnv = get.Optional.Field "enableEnv" Decode.bool
        envPath = get.Optional.Field "envPath" Decode.string |> Option.map UMX.tag<ServerUrl>
        dependencies = get.Optional.Field "" (Decode.list DependencyDecoder) |> Option.map Seq.ofList
        devDependencies = get.Optional.Field "" (Decode.list DependencyDecoder) |> Option.map Seq.ofList })

type Json =
  static member ToBytes value =
    JsonSerializer.SerializeToUtf8Bytes(value, DefaultJsonOptions())

  static member FromBytes<'T>(value: byte array) =
    JsonSerializer.Deserialize<'T>(ReadOnlySpan value, DefaultJsonOptions())

  static member ToText(value, ?minify) =
    let opts = DefaultJsonOptions()
    let minify = defaultArg minify false
    opts.WriteIndented <- minify
    JsonSerializer.Serialize(value, opts)

  static member ToNode value =
    JsonSerializer.SerializeToNode(value, DefaultJsonOptions())

  static member FromConfigFile(content: string) =
     Decode.fromString ConfigDecoders.PerlaDecoder content
