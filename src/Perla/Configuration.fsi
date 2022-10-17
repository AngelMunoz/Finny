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
    val FableConfig: FableConfig
    val DevServerConfig: DevServerConfig
    val EsbuildConfig: EsbuildConfig
    val BuildConfig: BuildConfig
    val PerlaConfig: PerlaConfig

module Json =
    open Perla.FileSystem
    open Perla.Json

    val getConfigDocument: unit -> JsonNode option
    val updateFileFields: jsonContents: byref<JsonNode option> -> fields: PerlaWritableField seq -> unit
    val fromFable: jsonNode: JsonNode -> config: PerlaConfig -> JsonNode * PerlaConfig
    val fromDevServer: jsonNode: JsonNode -> config: PerlaConfig -> JsonNode * PerlaConfig
    val fromEsbuildConfig: jsonNode: JsonNode -> config: PerlaConfig -> JsonNode * PerlaConfig
    val fromBuildConfig: jsonNode: JsonNode -> config: PerlaConfig -> JsonNode * PerlaConfig
    val fromPerla: jsonNode: JsonNode -> config: PerlaConfig -> PerlaConfig

val fromEnv: config: PerlaConfig -> PerlaConfig

val fromCli:
    runConfig: RunConfiguration option ->
    provider: Provider option ->
    serverOptions: DevServerField seq option ->
    config: PerlaConfig ->
        PerlaConfig

val fromFile: fileContent: JsonNode option -> config: PerlaConfig -> PerlaConfig

/// <summary>
/// </summary>
type Configuration =
    new: unit -> Configuration
    member CurrentConfig: PerlaConfig
    member UpdateFromCliArgs: ?runConfig: RunConfiguration * ?serverOptions: seq<DevServerField> -> unit
    member UpdateFromFile: unit -> unit
    member WriteFieldsToFile: newValues: seq<PerlaWritableField> -> unit

val Configuration: Configuration
