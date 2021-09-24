// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System.Threading.Tasks
open FSharp.Control.Tasks

open Argu

open FsToolkit.ErrorHandling
open Perla
open Perla.Types

type ServerArgs =
  | [<AltCommandLine("-a")>] Auto_Start of bool option
  | [<AltCommandLine("-p")>] Port of int option
  | [<AltCommandLine("-h")>] Host of string option
  | [<AltCommandLine("-s")>] Use_Ssl of bool option

  interface IArgParserTemplate with
    member this.Usage =
      match this with
      | Port _ -> "Select the server port, defaults to 7331"
      | Host _ -> "Server host, defaults to localhost"
      | Use_Ssl _ -> "Forces the requests to go through HTTPS. Defaults to true"
      | Auto_Start _ -> "Starts the server without action required by the user."

type BuildArgs =
  | [<AltCommandLine("-i")>] Index_File of string option
  | [<AltCommandLine("-ev")>] Esbuild_Version of string option
  | [<AltCommandLine("-o")>] Out_Dir of string option
  interface IArgParserTemplate with
    member this.Usage =
      match this with
      | Index_File _ ->
        "The Entry File for the web application. Defaults to \"index.html\""
      | Esbuild_Version _ ->
        "Use a specific esbuild version. defaults to \"0.12.9\""
      | Out_Dir _ -> "Where to output the files. Defaults to \"./dist\""

type InitArgs =
  | [<AltCommandLine("-p")>] Path of string
  | [<AltCommandLine("-wf")>] With_Fable of bool option

  static member ToOptions(args: ParseResults<InitArgs>) : InitOptions =
    { path = args.TryGetResult(Path)
      withFable = args.TryGetResult(With_Fable) |> Option.flatten }

  interface IArgParserTemplate with
    member this.Usage: string =
      match this with
      | Path _ -> "Where to write the config file"
      | With_Fable _ -> "Include fable options in the config file"

type SearchArgs =
  | [<AltCommandLine("-n")>] Name of string
  | [<AltCommandLine("-p")>] Page of int option

  static member ToOptions(args: ParseResults<SearchArgs>) : SearchOptions =
    { package = args.TryGetResult(Name)
      page = args.TryGetResult(Page) |> Option.flatten }

  interface IArgParserTemplate with
    member this.Usage: string =
      match this with
      | Name _ -> "The name of the package to search for."
      | Page _ -> "Page number to search at."

type ShowArgs =
  | [<AltCommandLine("-p")>] Package of string

  static member ToOptions(args: ParseResults<ShowArgs>) : ShowPackageOptions =
    { package = args.TryGetResult(Package) }

  interface IArgParserTemplate with
    member this.Usage: string =
      match this with
      | Package _ -> "The name of the package to show information about."

type RemoveArgs =
  | [<AltCommandLine("-p")>] Package of string

  static member ToOptions
    (args: ParseResults<RemoveArgs>)
    : RemovePackageOptions =
    { package = args.TryGetResult(Package) }

  interface IArgParserTemplate with
    member this.Usage: string =
      match this with
      | Package _ ->
        "The name of the package to remove from the import map this can also be aliased name."

type AddArgs =
  | [<AltCommandLine("-p")>] Package of string
  | [<AltCommandLine("-a")>] Alias of string option
  | [<AltCommandLine("-s")>] Source of Source option

  static member ToOptions(args: ParseResults<AddArgs>) : AddPackageOptions =
    { package = args.TryGetResult(Package)
      alias = args.TryGetResult(Alias) |> Option.flatten
      source = args.TryGetResult(Source) |> Option.flatten }

  interface IArgParserTemplate with
    member this.Usage: string =
      match this with
      | Package _ -> "The name of the package to show information about."
      | Alias _ -> "Specifier for this particular module."
      | Source _ ->
        "The name of the source you want to install a package from. e.g. unpkg or skypack."

type DevServerArgs =
  | [<CliPrefix(CliPrefix.None); AltCommandLine("s")>] Serve of
    ParseResults<ServerArgs>
  | [<CliPrefix(CliPrefix.None); AltCommandLine("b")>] Build of
    ParseResults<BuildArgs>
  | [<CliPrefix(CliPrefix.None)>] Init of ParseResults<InitArgs>
  | [<CliPrefix(CliPrefix.None); AltCommandLine("se")>] Search of
    ParseResults<SearchArgs>
  | [<CliPrefix(CliPrefix.None)>] Show of ParseResults<ShowArgs>
  | [<CliPrefix(CliPrefix.None)>] Add of ParseResults<AddArgs>
  | [<CliPrefix(CliPrefix.None)>] Remove of ParseResults<RemoveArgs>
  | [<AltCommandLine("-v")>] Version

  interface IArgParserTemplate with
    member this.Usage =
      match this with
      | Serve _ ->
        "Starts a development server for modern Javascript development"
      | Build _ -> "Builds the specified JS and CSS resources for production"
      | Init _ -> "Creates basic files and directories to start using fds."
      | Search _ -> "Searches a package in the skypack API."
      | Show _ -> "Gets the skypack information about a package."
      | Add _ -> "Generates an entry in the import map."
      | Remove _ -> "Removes an entry in the import map."
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
  let config =
    match Fs.getFdsConfig (Fs.Paths.GetFdsConfigPath()) with
    | Ok config -> config
    | Error err ->
      eprintfn "%s" err.Message
      FdsConfig.DefaultConfig()

  let devServerConfig =
    match config.devServer with
    | Some devServer -> devServer
    | None -> DevServerConfig.DefaultConfig()

  let foldServerOpts (server: DevServerConfig) (next: ServerArgs) =

    match next with
    | Auto_Start (Some value) -> { server with autoStart = Some value }
    | Port (Some value) -> { server with port = Some value }
    | Host (Some value) -> { server with host = Some value }
    | Use_Ssl (Some value) -> { server with useSSL = Some value }
    | _ -> server

  { config with
      devServer =
        serverargs
        |> List.fold foldServerOpts devServerConfig
        |> Some }

let getBuildOptions (serverargs: BuildArgs list) =
  let config =
    match Fs.getFdsConfig (Fs.Paths.GetFdsConfigPath()) with
    | Ok config -> config
    | Error err ->
      eprintfn "%s" err.Message
      FdsConfig.DefaultConfig()

  let buildConfig =
    match config.build with
    | Some build -> build
    | None -> BuildConfig.DefaultConfig()

  let foldBuildOptions (build: BuildConfig) (next: BuildArgs) =
    match next with
    | Esbuild_Version (Some value) ->
      { build with
          esbuildVersion = Some value }
    | Out_Dir (Some value) -> { build with outDir = Some value }
    | _ -> build

  { config with
      build =
        serverargs
        |> List.fold foldBuildOptions buildConfig
        |> Some }


[<EntryPoint>]
let main argv =
  taskResult {
    let parser = ArgumentParser.Create<DevServerArgs>()

    try
      let parsed =
        parser.ParseCommandLine(inputs = argv, raiseOnUsage = true)

      match parsed.TryGetSubCommand() with
      | Some (Init subcmd) ->
        return! subcmd |> InitArgs.ToOptions |> Commands.runInit
      | Some (Add subcmd) ->
        return! subcmd |> AddArgs.ToOptions |> Commands.runAdd
      | Some (Remove subcmd) ->
        return!
          subcmd
          |> RemoveArgs.ToOptions
          |> Commands.runRemove
      | Some (Build items) ->
        let buildConfig = getBuildOptions (items.GetAllResults())
        do! Commands.startBuild buildConfig :> Task
        return! Ok 0
      | Some (Serve items) ->
        let serverConfig = getServerOptions (items.GetAllResults())

        do!
          Commands.startInteractive serverConfig
          |> Async.Ignore

        return! Ok 0
      | Some (Search subcmd) ->
        return!
          subcmd
          |> SearchArgs.ToOptions
          |> Commands.runSearch
      | Some (Show subcmd) ->
        return! subcmd |> ShowArgs.ToOptions |> Commands.runShow
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
      | err ->
        parsed.Raise("No Commands Specified", showUsage = true)
        return! CommandNotParsedException $"%A{err}" |> Error
    with
    | ex -> return! ex |> Error
  }
  |> processExit
  |> Async.AwaitTask
  |> Async.RunSynchronously
