// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System.Threading.Tasks
open FSharp.Control.Tasks

open Argu

open FsToolkit.ErrorHandling
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


type InitArgs =
  | [<AltCommandLine("-p")>] Path of string

  static member ToOptions(args: ParseResults<InitArgs>) : InitOptions =
    { path = args.TryGetResult(Path) }

  interface IArgParserTemplate with
    member this.Usage: string =
      match this with
      | Path _ -> "Where to write the config file"

type SearchArgs =
  | [<AltCommandLine("-p")>] Package of string

  static member ToOptions(args: ParseResults<SearchArgs>) : SearchOptions =
    { package = args.TryGetResult(SearchArgs.Package) }

  interface IArgParserTemplate with
    member this.Usage: string =
      match this with
      | Package _ -> "The name of the package to search for."

type ShowArgs =
  | [<AltCommandLine("-p")>] Package of string

  static member ToOptions(args: ParseResults<ShowArgs>) : ShowPackageOptions =
    { package = args.TryGetResult(ShowArgs.Package) }

  interface IArgParserTemplate with
    member this.Usage: string =
      match this with
      | Package _ -> "The name of the package to show information about."

type UninstallArgs =
  | [<AltCommandLine("-p")>] Package of string

  static member ToOptions
    (args: ParseResults<UninstallArgs>)
    : UninstallPackageOptions =
    { package = args.TryGetResult(UninstallArgs.Package) }

  interface IArgParserTemplate with
    member this.Usage: string =
      match this with
      | Package _ ->
        "The name of the package to remove from the import map this can also be aliased name."

type InstallArgs =
  | [<AltCommandLine("-p")>] Package of string
  | [<AltCommandLine("-a")>] Alias of string option
  | [<AltCommandLine("-s")>] Source of Source option

  static member ToOptions
    (args: ParseResults<InstallArgs>)
    : InstallPackageOptions =
    { package = args.TryGetResult(InstallArgs.Package)
      alias =
        args.TryGetResult(InstallArgs.Alias)
        |> Option.flatten
      source =
        args.TryGetResult(InstallArgs.Source)
        |> Option.flatten }

  interface IArgParserTemplate with
    member this.Usage: string =
      match this with
      | Package _ -> "The name of the package to show information about."
      | Alias _ -> "Specifier for this particular module."
      | Source _ ->
        "The name of the source you want to install a package from. e.g. unpkg or skypack."

type SetEnvArgs =
  | [<AltCommandLine("-p")>] Env of Env

  static member ToOptions(args: ParseResults<SetEnvArgs>) : SetEnvOptions =
    { env = args.TryGetResult(Env) }

  interface IArgParserTemplate with
    member this.Usage: string =
      match this with
      | Env _ -> "Sets the export map for development/production."

type DevServerArgs =
  | [<CliPrefix(CliPrefix.None); AltCommandLine("s")>] Server of
    ParseResults<ServerArgs>
  | [<CliPrefix(CliPrefix.None); AltCommandLine("b")>] Build of
    ParseResults<BuildArgs>
  | [<CliPrefix(CliPrefix.None)>] Init of ParseResults<InitArgs>
  | [<CliPrefix(CliPrefix.None); AltCommandLine("se")>] Search of
    ParseResults<SearchArgs>
  | [<CliPrefix(CliPrefix.None)>] Show of ParseResults<ShowArgs>
  | [<CliPrefix(CliPrefix.None)>] Install of ParseResults<InstallArgs>
  | [<CliPrefix(CliPrefix.None)>] Uninstall of ParseResults<UninstallArgs>
  | [<AltCommandLine("-v")>] Version
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
      | Init _ -> "Creates basic files and directories to start using fds."
      | Search _ -> "Searches a package in the skypack API."
      | Show _ -> "Gets the skypack information about a package."
      | Install _ -> "Generates an entry in the import map."
      | Uninstall _ -> "Removes an entry in the import map."
      | Version _ -> "Prints out the cli version to the console."

let processExit (result: Task<Result<int, exn>>) =
  task {
    match! result with
    | Ok exitCode -> return exitCode
    | Error ex ->
      match ex with
      | CommandNotParsedException message -> eprintfn "%s" message
      | others -> eprintfn $"%s{others.Message}, at %s{others.Source}"

      return 1
  }


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
  taskResult {
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

        return! Ok 0
      | Some (Init subcmd) ->
        return! subcmd |> InitArgs.ToOptions |> Commands.runInit
      | Some (Search subcmd) ->
        return!
          subcmd
          |> SearchArgs.ToOptions
          |> Commands.runSearch
      | Some (Show subcmd) ->
        return! subcmd |> ShowArgs.ToOptions |> Commands.runShow
      | Some (Install subcmd) ->
        return!
          subcmd
          |> InstallArgs.ToOptions
          |> Commands.runInstall
      | Some (Uninstall subcmd) ->
        return!
          subcmd
          |> UninstallArgs.ToOptions
          |> Commands.runUninstall
      | Some Version ->
        printfn
          "%A"
          (System
            .Reflection
            .Assembly
            .GetEntryAssembly()
            .GetName()
            .Version)

        return! Ok 0
      | Some (Build items) ->
        let buildConfig = getBuildOptions (items.GetAllResults())
        do! Commands.startBuild (buildConfig, fableConfig) :> Task
        return! Ok 0
      | err ->
        parsed.Raise("No Commands Specified", showUsage = true)
        return! CommandNotParsedException $"%A{err}" |> Error
    with
    | ex -> return! ex |> Error
  }
  |> processExit
  |> Async.AwaitTask
  |> Async.RunSynchronously
