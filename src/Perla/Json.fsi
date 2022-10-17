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

    val PerlaDecoder: Decoder<DecodedPerlaConfig>

open ConfigDecoders

[<RequireQualifiedAccess; Struct>]
type PerlaConfigSection =
    | Index of index: string option
    | Fable of fable: FableConfig option
    | DevServer of devServer: DevServerConfig option
    | Build of build: BuildConfig option
    | Dependencies of dependencies: Dependency seq option
    | DevDependencies of devDependencies: Dependency seq option

val DefaultJsonOptions: unit -> JsonSerializerOptions
val DefaultJsonNodeOptions: unit -> JsonNodeOptions
val DefaultJsonDocumentOptions: unit -> JsonDocumentOptions

[<Class>]
type Json =
    static member ToBytes: value: 'a -> byte array
    static member FromBytes<'T> : value: byte array -> 'T
    static member ToText: value: 'a * ?minify: bool -> string
    static member ToNode: value: 'a -> JsonNode
    static member FromConfigFile: string -> Result<DecodedPerlaConfig,string>
