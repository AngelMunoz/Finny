// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open System.Threading.Tasks

open FSharp.Control.Tasks

open Argu

open FSharp.DevServer
open FSharp.DevServer.Types

type ServerArgs =
  | [<AltCommandLine("-a")>] Auto_Start of bool option
  | [<AltCommandLine("-p")>] Port of int option
  | [<AltCommandLine("-h")>] Host of string option
  | [<AltCommandLine("-d")>] Static_Files_Dir of string option
  | [<AltCommandLine("-s")>] Use_Ssl of bool option

  interface IArgParserTemplate with
    member this.Usage =
      match this with
      | Port _ -> "Select the server port, defaults to 7331"
      | Host _ -> "Server host, defaults to localhost"
      | Static_Files_Dir _ ->
        "Path to the static files directory, defaults to './public'"
      | Use_Ssl _ -> "Forces the requests to go through HTTPS. Defaults to true"
      | Auto_Start _ -> "Starts the server without action required by the user."

type BuildArgs =
  | [<AltCommandLine("-d")>] Static_Files_Dir of string option
  | [<AltCommandLine("-i")>] Index_File of string option
  | [<AltCommandLine("-ev")>] Esbuild_Version of string option
  | [<AltCommandLine("-o")>] Out_Dir of string option
  interface IArgParserTemplate with
    member this.Usage =
      match this with
      | Static_Files_Dir _ ->
        "Where to look for the JS and CSS Files. Defaults to \"./public\""
      | Index_File _ ->
        "The Entry File for the web application. Defaults to \"index.html\""
      | Esbuild_Version _ ->
        "Use a specific esbuild version. defaults to \"0.12.9\""
      | Out_Dir _ -> "Where to output the files. Defaults to \"./dist\""

type DevServerArgs =
  | [<CliPrefix(CliPrefix.None); AltCommandLine("s")>] Server of
    ParseResults<ServerArgs>
  | [<CliPrefix(CliPrefix.None); AltCommandLine("b")>] Build of
    ParseResults<BuildArgs>
  | [<AltCommandLine("-fa")>] Fable_Auto_start of bool option
  | [<AltCommandLine("-fp")>] Fable_Project of string option
  | [<AltCommandLine("-fe")>] Fable_Extension of string option
  | [<AltCommandLine("-fo")>] Fable_Out_Dir of string option

  interface IArgParserTemplate with
    member this.Usage =
      match this with
      | Server _ ->
        "Starts a development server for modern Javascript development"
      | Build _ -> "Builds the specified JS and CSS resources for production"
      | Fable_Auto_start _ ->
        "Auto-start fable in watch mode. Defaults to true, overrides the config file"
      | Fable_Project _ ->
        "The fsproject to use with fable. Defaults to \"./src/App.fsproj\", overrides the config file"
      | Fable_Extension _ ->
        "The extension to use with fable output files. Defaults to \".fs.js\", overrides the config file"
      | Fable_Out_Dir _ ->
        "Where to output the fable compiled files. Defaults to \"./public\", overrides the config file"


let getServerOptions (serverargs: ServerArgs list) =
  let serverOpts = DevServerConfig.DefaultConfig()

  let foldServerOpts (server: DevServerConfig) (next: ServerArgs) =

    match next with
    | Auto_Start (Some value) -> { server with AutoStart = Some value }
    | Port (Some value) -> { server with Port = Some value }
    | Host (Some value) -> { server with Host = Some value }
    | ServerArgs.Static_Files_Dir (Some value) ->
      { server with
          StaticFilesDir = Some value }
    | Use_Ssl (Some value) -> { server with UseSSL = Some value }
    | _ -> server

  serverargs |> List.fold foldServerOpts serverOpts

let getBuildOptions (serverargs: BuildArgs list) =
  let buildOpts = BuildConfig.DefaultConfig()

  let foldBuildOptions (build: BuildConfig) (next: BuildArgs) =
    match next with
    | Static_Files_Dir (Some value) ->
      { build with
          StaticFilesDir = Some value }
    | Index_File (Some value) -> { build with IndexFile = Some value }
    | Esbuild_Version (Some value) ->
      { build with
          EsbuildVersion = Some value }
    | Out_Dir (Some value) -> { build with OutDir = Some value }
    | _ -> build

  serverargs |> List.fold foldBuildOptions buildOpts


let getFableOptions (devServerArgs: DevServerArgs list) =
  let fableOpts = FableConfig.DefaultConfig()

  let foldFableOpts (fable: FableConfig) (next: DevServerArgs) =
    match next with
    | Fable_Auto_start (Some value) -> { fable with AutoStart = Some value }
    | Fable_Project (Some value) -> { fable with Project = Some value }
    | Fable_Extension (Some value) -> { fable with Extension = Some value }
    | Fable_Out_Dir (Some value) -> { fable with OutDir = Some value }
    | _ -> fable

  devServerArgs |> List.fold foldFableOpts fableOpts

[<EntryPoint>]
let main argv =
  task {
    let parser = ArgumentParser.Create<DevServerArgs>()

    try
      let parsed =
        parser.ParseCommandLine(inputs = argv, raiseOnUsage = true)

      let fableConfig = getFableOptions (parsed.GetAllResults())

      match parsed.TryGetSubCommand() with
      | Some (Server items) ->
        let serverConfig = getServerOptions (items.GetAllResults())

        do!
          Commands.startInteractive (serverConfig, fableConfig)
          |> Async.Ignore
      | Some (Build items) ->
        let buildConfig = getBuildOptions (items.GetAllResults())
        do! Commands.startBuild (buildConfig, fableConfig) :> Task
      | _ -> parsed.Raise("No Commands Specified", showUsage = true)
    with
    | ex -> eprintfn "%s" ex.Message
  }
  |> Async.AwaitTask
  |> Async.RunSynchronously

  0 // return an integer exit code
