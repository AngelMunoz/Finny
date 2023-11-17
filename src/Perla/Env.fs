namespace Perla.Environment

open System
open System.Collections
open System.Runtime.InteropServices
open System.Text.RegularExpressions

open FSharp.UMX
open FSharp.Data.Adaptive
open FsToolkit.ErrorHandling

open Perla.Units


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
type PlatformError =
  | UnsupportedPlatform
  | UnsupportedArch

[<Struct>]
type EnvVar = {
  Name: string
  Value: string
  Origin: EnvVarOrigin
  Target: EnvVarTarget
}

[<RequireQualifiedAccess>]
module PerlaArch =
  let AsString arch =
    match arch with
    | PerlaArch.X86 -> "ia32"
    | PerlaArch.X64 -> "x64"
    | PerlaArch.Arm -> "arm"
    | PerlaArch.Arm64 -> "arm64"

  let FromDotnetArch value =
    match value with
    | Architecture.X86 -> ValueSome PerlaArch.X86
    | Architecture.X64 -> ValueSome PerlaArch.X64
    | Architecture.Arm -> ValueSome PerlaArch.Arm
    | Architecture.Arm64 -> ValueSome PerlaArch.Arm64
    | _ -> ValueNone

[<RequireQualifiedAccess>]
module PerlaPlatform =
  let AsString platform =
    match platform with
    | PerlaPlatform.Windows -> "win32"
    | PerlaPlatform.Linux -> "linux"
    | PerlaPlatform.MacOS -> "darwin"

  let FromDotnetOsPlatform () =
    if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
      ValueSome PerlaPlatform.Windows
    else if RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
      ValueSome PerlaPlatform.Linux
    else if RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
      ValueSome PerlaPlatform.MacOS
    else
      ValueNone

[<RequireQualifiedAccess>]
module EnvLoader =
  [<Literal>]
  let PerlaClientEnvPrefix = "PERLA_"

  [<Literal>]
  let PerlaServerEnvPrefix = "PERLASRV_"

  let envVarRegex = Regex(@"^(?<key>[\w\d]+)(\s+)?=(\s+)?(?<value>[^\n\r]+)$")

  [<return: Struct>]
  let (|ClientPrefixed|_|) line =

    let groups = envVarRegex.Match(line).Groups
    let key = groups["key"]
    let value = groups["value"]

    if key.Success && key.Value.StartsWith(PerlaClientEnvPrefix) then
      ValueSome(key.Value, value.Value)
    else
      ValueNone

  [<return: Struct>]
  let (|ServerPrefixed|_|) line =

    let groups = envVarRegex.Match(line).Groups
    let key = groups["key"]
    let value = groups["value"]

    if key.Success && key.Value.StartsWith(PerlaServerEnvPrefix) then
      ValueSome(key.Value, value.Value)
    else
      ValueNone

  type LoadEnvFiles<'FileSystem
    when 'FileSystem: (static member ReadAllLines: path: string -> string array)>
    = 'FileSystem

  type ReadFromEnv<'T
    when 'T: (static member GetEnvironmentVariables:
      unit -> Collections.IDictionary)> = 'T

  let inline matchLine origin line =
    match line with
    | ClientPrefixed(varName, varContent) ->
      {
        Name = varName.Replace(PerlaClientEnvPrefix, String.Empty)
        Value = varContent
        Origin = origin
        Target = EnvVarTarget.Client
      }
      |> Some
    | ServerPrefixed(varName, varContent) ->
      {
        Name = varName.Replace(PerlaServerEnvPrefix, String.Empty)
        Value = varContent
        Origin = origin
        Target = EnvVarTarget.PerlaServer
      }
      |> Some
    | _ -> None

  let inline LoadEnvFiles<'Fs when LoadEnvFiles<'Fs>> files =
    let inline collectLines (path: string<SystemPath>) =
      'Fs.ReadAllLines(UMX.untag path)
      |> Array.Parallel.choose (matchLine (EnvVarOrigin.File path))

    files |> Seq.collect collectLines |> List.ofSeq

  let inline LoadFromSystem<'T when ReadFromEnv<'T>> () =

    let builder = ResizeArray()

    let envVars = 'T.GetEnvironmentVariables()

    for entry in envVars do
      let entry = entry :?> DictionaryEntry

      match matchLine EnvVarOrigin.System $"{entry.Key}={entry.Value}" with
      | Some var -> builder.Add(var)
      | None -> ()

    builder |> List.ofSeq

[<RequireQualifiedAccess>]
module PerlaEnvironment =
  type GetEnvVars<'T when 'T: (member EnvVars: EnvVar alist)> = 'T

  type GetArch<'T when 'T: (member CurrentArch: PerlaArch voption)> = 'T

  type GetPlatformAndArch<'T
    when 'T: (member CurrentPlatform: PerlaPlatform voption) and GetArch<'T>> =
    'T

  type IsWindows<'T when 'T: (member IsWindows: bool)> = 'T

  let inline GetEnvVars<'Env when GetEnvVars<'Env>> (env: 'Env) kind =
    env.EnvVars
    |> AList.choose (fun var -> if var.Target = kind then Some var else None)

  let inline GetPlatform<'Env when GetPlatformAndArch<'Env>> (env: 'Env) = result {
    let! platform =
      env.CurrentPlatform |> Result.requireVSome UnsupportedPlatform

    let! arch = env.CurrentArch |> Result.requireVSome UnsupportedArch

    return struct (platform, arch)
  }

  let inline IsWindows<'Env when IsWindows<'Env>> (env: 'Env) = env.IsWindows


  let ScriptFromEnv (envVars: EnvVar alist) = adaptive {
    let! value =
      envVars
      |> AList.fold
        (fun acc var -> acc + $"""export const {var.Name} = "{var.Value}";""")
        ""

    if String.IsNullOrWhiteSpace value then
      return ValueNone
    else
      return ValueSome value
  }
