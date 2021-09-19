namespace FSharp.DevServer

open System
open FsToolkit.ErrorHandling
open Types

[<RequireQualifiedAccess>]
module Env =
  open System.IO
  open System.Runtime.InteropServices

  [<Literal>]
  let FdsDirectoryName = ".fsdevserver"

  let getToolsPath () =
    let user =
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)

    Path.Combine(user, FdsDirectoryName)

  let isWindows =
    RuntimeInformation.IsOSPlatform(OSPlatform.Windows)

  let platformString =
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

  let archString =
    match RuntimeInformation.OSArchitecture with
    | Architecture.Arm -> "arm"
    | Architecture.Arm64 -> "arm64"
    | Architecture.X64 -> "64"
    | Architecture.X86 -> "32"
    | _ -> failwith "Unsupported Architecture"

[<RequireQualifiedAccess>]
module internal Json =
  open System.Text.Json
  open System.Text.Json.Serialization

  let private jsonOptions =
    let opts = JsonSerializerOptions()
    opts.WriteIndented <- true
    opts.AllowTrailingCommas <- true
    opts.ReadCommentHandling <- JsonCommentHandling.Skip
    opts.UnknownTypeHandling <- JsonUnknownTypeHandling.JsonElement
    opts.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
    opts

  let ToBytes value =
    JsonSerializer.SerializeToUtf8Bytes(value, jsonOptions)

  let FromBytes<'T> (bytes: byte array) =
    JsonSerializer.Deserialize<'T>(ReadOnlySpan bytes, jsonOptions)

  let ToText value =
    JsonSerializer.Serialize(value, jsonOptions)

[<RequireQualifiedAccessAttribute>]
module internal Http =
  open Flurl.Http

  [<Literal>]
  let SKYPACK_CDN = "https://cdn.skypack.dev"

  let getPackageUrlInfo name =
    taskResult {
      try
        let info = {| lookUp = $"%s{name}" |}
        let! res = $"{SKYPACK_CDN}/{info.lookUp}".GetAsync()

        if res.StatusCode >= 400 then
          return! PackageNotFoundException |> Error

        let mutable pinnedUrl = ""
        let mutable importUrl = ""

        let info =
          if
            res.Headers.TryGetFirst("x-pinned-url", &pinnedUrl)
            |> not
          then
            {| info with pin = None |}
          else
            {| info with pin = Some pinnedUrl |}

        let info =
          if
            res.Headers.TryGetFirst("x-import-url", &importUrl)
            |> not
          then
            {| info with import = None |}
          else
            {| info with import = Some importUrl |}

        return
          { lookUp = info.lookUp
            pin = info.pin |> Option.defaultValue info.lookUp
            import = info.import |> Option.defaultValue info.lookUp }

      with
      | :? Flurl.Http.FlurlHttpException ->
        return! PackageNotFoundException |> Error
    }

[<RequireQualifiedAccess>]
module Fs =
  open System.IO

  [<Literal>]
  let FdsConfigName = "fds.jsonc"

  type Paths() =
    static member GetFdsConfigPath(?path: string) =
      $"{defaultArg path (Environment.CurrentDirectory)}/{FdsConfigName}"

  let getFdsConfig filepath =
    try
      let bytes = File.ReadAllBytes filepath
      Json.FromBytes<FdsConfig> bytes |> Ok
    with
    | ex -> ex |> Error

  let ensureParentDirectory path =
    try
      Directory.CreateDirectory(path) |> ignore |> Ok
    with
    | ex -> ex |> Error

  let createFdsConfig path config =
    let serialized = Json.ToBytes config

    try
      File.WriteAllBytes(path, serialized) |> Ok
    with
    | ex -> Error ex

  let getorCreateLockFile configPath =
    taskResult {
      try
        let path = Path.GetFullPath($"%s{configPath}.lock")

        do! ensureParentDirectory (Path.GetDirectoryName(path))

        let bytes = File.ReadAllBytes(path)

        return Json.FromBytes<PacakgesLock> bytes
      with
      | :? System.IO.FileNotFoundException -> return Map.ofList []
      | ex -> return! ex |> Error
    }

  let writeLockFile configPath (fdsLock: PacakgesLock) =
    let path = Path.GetFullPath($"%s{configPath}.lock")
    let serialized = Json.ToBytes fdsLock

    try
      File.WriteAllBytes(path, serialized) |> Ok
    with
    | ex -> Error ex



  let getOrCreateImportMap path =
    taskResult {
      try
        let path = Path.GetFullPath(path)

        do! ensureParentDirectory (Path.GetDirectoryName(path))

        let bytes = File.ReadAllBytes(path)

        return Json.FromBytes<ImportMap> bytes
      with
      | :? System.IO.FileNotFoundException ->
        return
          { imports = Map.ofSeq Seq.empty
            scopes = Map.ofSeq Seq.empty }
      | ex -> return! ex |> Error
    }

  let writeImportMap path importMap =
    result {
      let bytes = Json.ToBytes importMap

      try
        let path = Path.GetFullPath(path)

        Directory.CreateDirectory(Path.GetDirectoryName(path))
        |> ignore

        File.WriteAllBytes(path, bytes)
      with
      | ex -> return! ex |> Error
    }
