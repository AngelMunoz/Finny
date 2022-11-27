[<RequireQualifiedAccess>]
module Perla.Env

val IsWindows: bool
val PlatformString: string
val ArchString: string
val internal getPerlaEnvVars: unit -> (string * string) list
val GetEnvContent: unit -> string option
