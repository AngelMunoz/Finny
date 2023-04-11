[<RequireQualifiedAccess>]
module Perla.Commands

open System.CommandLine

val Setup: Command
val Template: Command
val Describe: Command

val Build: Command
val Serve: Command
val Test: Command

val SearchPackage: Command
val ShowPackage: Command
val AddPackage: Command
val RemovePackage: Command
val ListPackages: Command
val RestoreImportMap: Command

val NewProject: Command
