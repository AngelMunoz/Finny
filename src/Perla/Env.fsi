[<RequireQualifiedAccess>]
module Perla.Env

open System
open System.Collections
open System.Runtime.InteropServices
open System.Text
open FsToolkit.ErrorHandling

val IsWindows: bool
val PlatformString: string
val ArchString: string
val GetEnvContent: unit -> string option
