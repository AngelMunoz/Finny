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
