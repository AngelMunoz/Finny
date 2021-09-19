namespace FSharp.DevServer


open System
open FSharp.Control
open FSharp.Control.Tasks
open System.IO
open FsToolkit.ErrorHandling

open Types
open Server
open Build

open type Fs.Paths

module Commands =

  let startInteractive (configuration: DevServerConfig * FableConfig) =
    let devConfig, fableConfig = configuration
    let onStdinAsync = serverActions devConfig fableConfig

    let autoStartServer = defaultArg devConfig.AutoStart true
    let autoStartFable = defaultArg fableConfig.AutoStart true

    asyncSeq {
      if autoStartServer then "start"
      if autoStartFable then "start:fable"

      while true do
        let! value = Console.In.ReadLineAsync() |> Async.AwaitTask
        value
    }
    |> AsyncSeq.distinctUntilChanged
    |> AsyncSeq.iterAsync onStdinAsync

  let startBuild (configuration: BuildConfig * FableConfig) =
    let buildConfig, fableConfig = configuration
    execBuild buildConfig fableConfig


  let private (|ScopedPackage|Package|) (package: string) =
    if package.StartsWith("@") then
      ScopedPackage(package.Substring(1))
    else
      Package package

  let private parsePackageName (name: string) =

    let getVersion parts =

      let version =
        let version =
          parts |> Seq.tryLast |> Option.defaultValue ""

        if String.IsNullOrWhiteSpace version then
          None
        else
          Some version

      version

    match name with
    | ScopedPackage name ->
      // check if the user is looking to install a particular version
      // i.e. package@5.0.0
      if name.Contains("@") then
        let parts = name.Split("@")
        let version = getVersion parts

        $"@{parts.[0]}", version
      else
        $"@{name}", None
    | Package name ->
      if name.Contains("@") then
        let parts = name.Split("@")

        let version = getVersion parts
        parts.[0], version
      else
        name, None

  let runInit options =
    result {
      let path =
        match options.path with
        | Some path -> GetFdsConfigPath(path)
        | None -> GetFdsConfigPath()

      let config =
        { name = ""
          importMapPath = "./wwwroot/imports.importmap" |> Some
          dependencies = Map.ofSeq (Seq.empty<string * string>) |> Some }

      do! Fs.createFdsConfig path config

      return 0
    }

  let runSearch options =
    result {
      printfn "%A" options
      return 0
    }

  let runShow options =
    result {
      printfn "%A" options
      return 0
    }

  let runUninstall (options: UninstallPackageOptions) =
    taskResult {
      let name = defaultArg options.package ""

      if name = "" then
        return! PackageNotFoundException |> Error

      let! opts = Fs.getFdsConfig (GetFdsConfigPath())

      let! path =
        match opts.importMapPath with
        | Some path -> path |> Ok
        | None -> MissingImportMapPathException |> Error

      let! map = Fs.getOrCreateImportMap path
      let! lockFile = Fs.getorCreateLockFile (GetFdsConfigPath())

      let imports = map.imports |> Map.remove name

      let deps =
        opts.dependencies
        |> Option.map (fun map -> map |> Map.remove name)

      let map = { map with imports = imports }
      let opts = { opts with dependencies = deps }
      let lockFile = lockFile |> Map.remove name

      do! Fs.writeImportMap path map
      do! Fs.writeLockFile (GetFdsConfigPath()) lockFile
      do! Fs.createFdsConfig (GetFdsConfigPath()) opts

      return 0
    }

  let runInstall (options: InstallPackageOptions) =
    taskResult {
      let! package, version =
        match options.package with
        | Some package -> parsePackageName package |> Ok
        | None -> MissingPackageNameException |> Error

      let alias =
        options.alias |> Option.defaultValue package

      let version =
        match version with
        | Some version -> $"@{version}"
        | None -> ""

      let! info = Http.getPackageUrlInfo $"{package}{version}"

      let! opts = Fs.getFdsConfig (GetFdsConfigPath())
      let! lockFile = Fs.getorCreateLockFile (GetFdsConfigPath())

      let dependencies =
        opts.dependencies
        |> Option.defaultValue (Map.ofList [])
        |> Map.change
             alias
             (fun f ->
               f
               |> Option.map (fun _ -> $"{Http.SKYPACK_CDN}/{info.lookUp}")
               |> Option.orElse (Some $"{Http.SKYPACK_CDN}/{info.lookUp}"))

      let opts =
        { opts with
            dependencies = Some dependencies }

      let lockFile =
        lockFile
        |> Map.change
             alias
             (fun f ->
               f
               |> Option.map (fun _ -> info)
               |> Option.orElse (Some info))

      let! importMapPath =
        match opts.importMapPath with
        | Some path -> path |> Ok
        | None -> MissingImportMapPathException |> Error

      let! map = Fs.getOrCreateImportMap importMapPath

      let imports =
        map.imports
        |> Map.change
             alias
             (fun v ->
               v
               |> Option.map (fun _ -> $"{Http.SKYPACK_CDN}/{info.lookUp}")
               |> Option.orElse (Some $"{Http.SKYPACK_CDN}/{info.lookUp}"))

      let map = { map with imports = imports }

      do! Fs.createFdsConfig (GetFdsConfigPath()) opts
      do! Fs.writeImportMap importMapPath map
      do! Fs.writeLockFile (GetFdsConfigPath()) lockFile

      return 0
    }
