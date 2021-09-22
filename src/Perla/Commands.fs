namespace Perla

open System
open FSharp.Control
open FsToolkit.ErrorHandling

open Types
open Server
open Build

open type Fs.Paths

module Commands =

  let startInteractive (configuration: FdsConfig) =
    let onStdinAsync = serverActions configuration

    let deServer =
      defaultArg configuration.devServer (DevServerConfig.DefaultConfig())

    let fableConfig =
      defaultArg configuration.fable (FableConfig.DefaultConfig())

    let autoStartServer = defaultArg deServer.autoStart true
    let autoStartFable = defaultArg fableConfig.autoStart true

    asyncSeq {
      if autoStartServer then "start"
      if autoStartFable then "start:fable"

      while true do
        let! value = Console.In.ReadLineAsync() |> Async.AwaitTask
        value
    }
    |> AsyncSeq.iterAsync onStdinAsync

  let startBuild (configuration: FdsConfig) = execBuild configuration


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
        FdsConfig.DefaultConfig(defaultArg options.withFable false)

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

  let runRemove (options: RemovePackageOptions) =
    taskResult {
      let name = defaultArg options.package ""

      if name = "" then
        return! PackageNotFoundException |> Error

      let! fdsConfig = Fs.getFdsConfig (GetFdsConfigPath())
      let! lockFile = Fs.getorCreateLockFile (GetFdsConfigPath())

      let deps =
        fdsConfig.packages
        |> Option.map (fun map -> map |> Map.remove name)

      let opts = { fdsConfig with packages = deps }

      let lockFile = lockFile |> Map.remove name

      do! Fs.writeLockFile (GetFdsConfigPath()) lockFile
      do! Fs.createFdsConfig (GetFdsConfigPath()) opts

      return 0
    }

  let runAdd (options: AddPackageOptions) =
    taskResult {
      let! package, version =
        match options.package with
        | Some package -> parsePackageName package |> Ok
        | None -> MissingPackageNameException |> Error

      let alias =
        options.alias |> Option.defaultValue package

      let source = defaultArg options.source Source.Skypack

      let version =
        match version with
        | Some version -> $"@{version}"
        | None -> ""

      let! info = Http.getPackageUrlInfo $"{package}{version}" source

      let! fdsConfig = Fs.getFdsConfig (GetFdsConfigPath())
      let! lockFile = Fs.getorCreateLockFile (GetFdsConfigPath())

      let packages =
        fdsConfig.packages
        |> Option.defaultValue Map.empty
        |> (Map.change
              alias
              (fun entry ->
                entry
                |> Option.map (fun _ -> info.import)
                |> Option.orElse (Some info.import)))

      let fdsConfig =
        { fdsConfig with
            packages = packages |> Some }

      let lockFile =
        lockFile
        |> Map.change
             alias
             (fun f ->
               f
               |> Option.map (fun _ -> info)
               |> Option.orElse (Some info))

      do! Fs.createFdsConfig (GetFdsConfigPath()) fdsConfig
      do! Fs.writeLockFile (GetFdsConfigPath()) lockFile

      return 0
    }
