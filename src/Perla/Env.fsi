namespace Perla.Environment

open FSharp.UMX
open Perla.Units
open FSharp.Data.Adaptive

[<Struct; RequireQualifiedAccess>]
type PerlaPlatform =
  | Windows
  | Linux
  | MacOS

[<Struct; RequireQualifiedAccess>]
type PerlaArch =
  | X86
  | X64
  | Arm
  | Arm64

[<Struct; RequireQualifiedAccess>]
type EnvVarOrigin =
  | File of string<SystemPath>
  | System
  | CLI
  | Config

[<Struct; RequireQualifiedAccess>]
type EnvVarTarget =
  | Client
  | PerlaServer

[<Struct>]
type EnvVar = {
  Name: string
  Value: string
  Origin: EnvVarOrigin
  Target: EnvVarTarget
}

[<Struct>]
type PlatformError =
  | UnsupportedPlatform
  | UnsupportedArch

[<RequireQualifiedAccess>]
module PerlaArch =
  val AsString: PerlaArch -> string

[<RequireQualifiedAccess>]
module PerlaPlatform =
  val AsString: PerlaPlatform -> string


[<RequireQualifiedAccess>]
module EnvLoader =

  type LoadEnvFiles<'FileSystem
    when 'FileSystem: (static member ReadAllLines: path: string -> string array)>
    = 'FileSystem

  type ReadFromEnv<'T
    when 'T: (static member GetEnvironmentVariables:
      unit -> System.Collections.IDictionary)> = 'T

  val inline LoadEnvFiles<'Fs when LoadEnvFiles<'Fs>> :
    files: string<SystemPath> seq -> EnvVar list

  val inline LoadFromSystem<'T when ReadFromEnv<'T>> : unit -> EnvVar list

[<RequireQualifiedAccess>]
module PerlaEnvironment =
  type GetEnvVars<'T when 'T: (member EnvVars: EnvVar alist)> = 'T

  type GetArch<'T when 'T: (member CurrentArch: PerlaArch voption)> = 'T

  type GetPlatformAndArch<'T
    when 'T: (member CurrentPlatform: PerlaPlatform voption) and GetArch<'T>> =
    'T

  type IsWindows<'T when 'T: (member IsWindows: bool)> = 'T

  val inline GetEnvVars<'Env when GetEnvVars<'Env>> :
    env: 'Env -> kind: EnvVarTarget -> EnvVar alist

  val inline GetPlatform<'Env when GetPlatformAndArch<'Env>> :
    env: 'Env -> Result<struct (PerlaPlatform * PerlaArch), PlatformError>

  val inline IsWindows<'Env when IsWindows<'Env>> : env: 'Env -> bool


  val ScriptFromEnv: EnvVar alist -> aval<string ValueOption>
