[<RequireQualifiedAccess>]
module Perla.Env

open FSharp.UMX
open Perla.Units

val IsWindows: bool
val PlatformString: string
val ArchString: string
val internal getPerlaEnvVars: unit -> (string * string) list
val GetEnvContent: unit -> string option
val LoadDotEnv: files: string<SystemPath> seq -> unit
