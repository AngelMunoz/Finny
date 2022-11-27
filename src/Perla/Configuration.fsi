module Perla.Configuration

open FSharp.UMX
open Perla.Types
open Perla.Units
open Perla.PackageManager.Types
open System.Runtime.InteropServices

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

/// <summary>
/// </summary>
type Configuration =
    new: unit -> Configuration
    member CurrentConfig: PerlaConfig
    member UpdateFromCliArgs:
        [<Optional>] ?runConfig: RunConfiguration *
        [<Optional>] ?provider: Provider *
        [<Optional>] ?serverOptions: seq<DevServerField> ->
            unit
    member UpdateFromFile: unit -> unit
    member WriteFieldsToFile: newValues: seq<PerlaWritableField> -> unit

val Configuration: Configuration
