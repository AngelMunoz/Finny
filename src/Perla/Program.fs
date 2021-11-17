// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp
open System.Threading.Tasks

open Argu

open FsToolkit.ErrorHandling
open Perla
open Perla.Types


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

[<EntryPoint>]
let main argv =
  taskResult {
    let parser = ArgumentParser.Create<DevServerArgs>()

    try
      let parsed = parser.ParseCommandLine(inputs = argv, raiseOnUsage = true)

      match parsed.TryGetResult(Version) with
      | Some Version ->
        let version =
          System
            .Reflection
            .Assembly
            .GetEntryAssembly()
            .GetName()
            .Version

        printfn $"{version.Major}.{version.Minor}.{version.Build}"
        return! Ok 0
      | _ ->
        match parsed.TryGetSubCommand() with
        | Some (Build items) ->
          let buildConfig = Commands.getBuildOptions (items.GetAllResults())

          Fs.Paths.SetCurrentDirectoryToPerlaConfigDirectory()
          do! Commands.startBuild buildConfig :> Task
          return! Ok 0
        | Some (Serve items) ->
          let serverConfig = Commands.getServerOptions (items.GetAllResults())

          Fs.Paths.SetCurrentDirectoryToPerlaConfigDirectory()
          do! Commands.startInteractive serverConfig

          return! Ok 0
        | Some (Init subcmd) ->
          return! subcmd |> InitArgs.ToOptions |> Commands.runInit
        | Some (Add subcmd) ->
          return! subcmd |> AddArgs.ToOptions |> Commands.runAdd
        | Some (Remove subcmd) ->
          return!
            subcmd
            |> RemoveArgs.ToOptions
            |> Commands.runRemove
        | Some (Search subcmd) ->
          return!
            subcmd
            |> SearchArgs.ToOptions
            |> Commands.runSearch
        | Some (Show subcmd) ->
          return! subcmd |> ShowArgs.ToOptions |> Commands.runShow
        | Some (List subcmd) ->
          return! subcmd |> ListArgs.ToOptions |> Commands.runList
        | err ->
          parsed.Raise("No Commands Specified", showUsage = true)
          return! CommandNotParsedException $"%A{err}" |> Error
    with
    | ex -> return! ex |> Error
  }
  |> processExit
  |> Async.AwaitTask
  |> Async.RunSynchronously
