module Perla.Configuration

open Perla.Types
open Perla.PackageManager.Types
open System.Runtime.InteropServices

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Types =
    type DevServerField =
        | Port of int
        | Host of string
        | LiveReload of bool
        | UseSSl of bool

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

/// <summary>
/// </summary>
[<Class>]
type Configuration =
    new: unit -> Configuration
    member CurrentConfig: PerlaConfig
    member UpdateFromCliArgs:
        [<Optional>] ?runConfig: RunConfiguration *
        [<Optional>] ?provider: Provider *
        [<Optional>] ?serverOptions: seq<DevServerField> *
        [<Optional>] ?testingOptions: seq<TestingField> ->
            unit
    member UpdateFromFile: unit -> unit
    member WriteFieldsToFile: newValues: seq<PerlaWritableField> -> unit

val Configuration: Configuration
