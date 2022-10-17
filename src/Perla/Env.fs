[<RequireQualifiedAccess>]
module Perla.Env

open System
open System.Collections
open System.Runtime.InteropServices
open System.Text

open FsToolkit.ErrorHandling

let IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)

let PlatformString =
  if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
    "windows"
  else if RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
    "linux"
  else if RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
    "darwin"
  else if RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD) then
    "freebsd"
  else
    failwith "Unsupported OS"

let ArchString =
  match RuntimeInformation.OSArchitecture with
  | Architecture.Arm -> "arm"
  | Architecture.Arm64 -> "arm64"
  | Architecture.X64 -> "64"
  | Architecture.X86 -> "32"
  | _ -> failwith "Unsupported Architecture"

let internal getPerlaEnvVars () =
  let env = Environment.GetEnvironmentVariables()
  let prefix = "PERLA_"

  [ for entry in env do
      let entry = entry :?> DictionaryEntry
      let key = entry.Key :?> string
      let value = entry.Value :?> string

      if key.StartsWith(prefix) then
        (key.Replace(prefix, String.Empty), value) ]

let GetEnvContent () =
  option {
    let env = getPerlaEnvVars ()
    let sb = StringBuilder()

    for key, value in env do
      sb.Append($"""export const {key} = "{value}";""") |> ignore

    let content = sb.ToString()

    if String.IsNullOrWhiteSpace content then
      return! None
    else
      return content
  }
