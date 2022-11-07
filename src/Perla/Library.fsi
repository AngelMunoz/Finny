namespace Perla
open System.Text.RegularExpressions

[<AutoOpen>]
module Lib =
    open Perla.Types
    open Perla.PackageManager.Types
    open Spectre.Console

    val internal (|ParseRegex|_|): regex: Regex -> str: string -> string list option
    val internal ExtractDependencyInfoFromUrl: url: string -> (Provider * string * string) option
    val internal parseFullRepositoryName: value: string -> (string * string * string) option
    val internal getTemplateAndChild: templateName: string -> string option * string * string option
    val internal dependencyTable: deps: seq<Dependency> * title: string -> Table
    val internal (|ScopedPackage|Package|): package: string -> Choice<string, string>
    val internal parsePackageName: name: string -> string * string option
    val internal (|Log|Debug|Info|Err|Warning|Clear|): string -> Choice<unit, unit, unit, unit, unit, unit>
