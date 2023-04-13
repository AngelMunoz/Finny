module Perla.Configuration

open Perla.Types
open Perla.Units
open FSharp.UMX
open Perla.PackageManager.Types
open System.Runtime.InteropServices
open System.Text.Json.Nodes

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


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Defaults =
  val FableConfig: FableConfig
  val DevServerConfig: DevServerConfig
  val EsbuildConfig: EsbuildConfig
  val BuildConfig: BuildConfig
  val TestConfig: TestConfig
  val PerlaConfig: PerlaConfig

val internal fromEnv: config: PerlaConfig -> PerlaConfig

val internal fromCli:
  runConfig: RunConfiguration option ->
  provider: Provider option ->
  serverOptions: DevServerField seq option ->
  testingOptions: TestingField seq option ->
  config: PerlaConfig ->
    PerlaConfig

val internal fromFile:
  fileContent: JsonObject option -> config: PerlaConfig -> PerlaConfig

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal Json =

  val getConfigDocument: perlaJsonText: string -> JsonObject

/// <summary>
/// Represents a store for the perla configuration for the current session
/// This class ensures there's a central way to update perla's configuration
/// either by CLI, or the perla.json file, this also ensures some of the
/// configuration fields are written correctly to perla.json
/// </summary>
[<Class>]
type ConfigurationManager =
  new:
    readPerlaJsonText: (unit -> string option) *
    writePerlaJsonText: (JsonObject option -> unit) ->
      ConfigurationManager

  member CurrentConfig: PerlaConfig

  member UpdateFromCliArgs:
    [<Optional>] ?runConfig: RunConfiguration *
    [<Optional>] ?provider: Provider *
    [<Optional>] ?serverOptions: seq<DevServerField> *
    [<Optional>] ?testingOptions: seq<TestingField> ->
      unit

  member UpdateFromFile: unit -> unit
  member WriteFieldsToFile: newValues: seq<PerlaWritableField> -> unit

val ConfigurationManager: ConfigurationManager
