namespace Perla

[<AutoOpen>]
module Lib =
    open System
    open System.Text
    open Perla.Types
    open Perla.PackageManager.Types
    open Perla.Scaffolding
    open Spectre.Console

    val (|ParseRegex|_|): regex: string -> str: string -> string list option
    val ExtractDependencyInfoFromUrl: url: string -> (Provider * string * string) option
    val (|RestartFable|StartFable|StopFable|UnknownFable|): (string -> Choice<unit, unit, unit, string>)

    val (|RestartServer|StartServer|StopServer|Clear|Exit|Unknown|):
        (string -> Choice<unit, unit, unit, unit, unit, string>)

    val (|Typescript|Javascript|Jsx|Css|Json|Other|): value: string -> Choice<unit, unit, unit, unit, unit, string>
    val (|ScopedPackage|Package|): package: string -> Choice<string, string>
    val parsePackageName: name: string -> string * string option
    val getRepositoryName: fullRepoName: string -> Result<string, NameParsingErrors>
    val getTemplateAndChild: templateName: string -> string option * string * string option
    val dependencyTable: deps: seq<Dependency> * title: string -> Table
