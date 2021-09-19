namespace FSharp.DevServer

module Types =

  let (|RestartFable|StartFable|StopFable|UnknownFable|) =
    function
    | "restart:fable" -> RestartFable
    | "start:fable" -> StartFable
    | "stop:fable" -> StopFable
    | value -> UnknownFable value

  let (|RestartServer|StartServer|StopServer|Clear|Exit|Unknown|) =
    function
    | "restart" -> RestartServer
    | "start" -> StartServer
    | "stop" -> StopServer
    | "clear"
    | "cls" -> Clear
    | "exit"
    | "stop" -> Exit
    | value -> Unknown value


  type FableConfig =
    { AutoStart: bool option
      Project: string option
      Extension: string option
      OutDir: string option }

    static member DefaultConfig() =
      { AutoStart = Some true
        Project = Some "./src/App.fsproj"
        Extension = Some ".fs.js"
        OutDir = Some "./public" }

  type DevServerConfig =
    { AutoStart: bool option
      Port: int option
      Host: string option
      StaticFilesDir: string option
      UseSSL: bool option }

    static member DefaultConfig() =
      { AutoStart = Some true
        Port = Some 7331
        Host = None
        StaticFilesDir = Some "./public"
        UseSSL = Some true }

  type BuildConfig =
    { StaticFilesDir: string option
      IndexFile: string option
      EsbuildVersion: string option
      OutDir: string option }

    static member DefaultConfig() =
      { StaticFilesDir = Some "./public"
        IndexFile = Some "index.html"
        EsbuildVersion = Some "0.12.9"
        OutDir = Some "./dist" }

  type LockDependency =
    { lookUp: string
      pin: string
      import: string }

  type FdsConfig =
    { name: string
      importMapPath: string option
      dependencies: Map<string, string> option }

  type FdsLock = Map<string, LockDependency>

  type ImportMap =
    { imports: Map<string, string>
      scopes: Map<string, string> }

  type Source =
    | Skypack = 0

  type Env =
    | Dev = 0
    | Prod = 1

  type InitOptions = { path: string option }

  // https://api.skypack.dev/v1/search?q=package-name&p=1
  type SearchOptions = { package: string option }

  // https://api.skypack.dev/v1/package/package-name
  type ShowPackageOptions = { package: string option }

  // https://cdn.skypack.dev/package-name
  type InstallPackageOptions =
    { package: string option
      alias: string option
      source: Source option }

  type UninstallPackageOptions = { package: string option }

  type SetEnvOptions = { env: Env option }

  exception CommandNotParsedException of string
  exception HelpRequestedException
  exception MissingPackageNameException
  exception MissingImportMapPathException
  exception PackageNotFoundException
  exception HeaderNotFoundException of string
