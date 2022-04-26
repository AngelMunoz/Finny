// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp
open System.Threading.Tasks

open System
open Argu
open FsToolkit.ErrorHandling
open Perla
open Perla.Lib
open Types
open Logger

let processExit (result: Task<Result<int, exn>>) =
  task {
    match! result with
    | Ok exitCode -> return exitCode
    | Error ex ->
      match ex with
      | :? ArguParseException as ex -> eprintfn $"{ex.Message}"
      | CommandNotParsedException message -> eprintfn $"{message}"
      | others ->
        Logger.log ($"There was an error running this command", others)

      return 1
  }

[<EntryPoint>]
let main argv =
  taskResult {
    let parser = ArgumentParser.Create<DevServerArgs>()
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development")

    try
      let parsed =
        parser.ParseCommandLine(
          inputs = argv,
          raiseOnUsage = true,
          ignoreMissing = true
        )

      match parsed.GetAllResults() with
      | [ Version ] ->
        let version =
          System
            .Reflection
            .Assembly
            .GetEntryAssembly()
            .GetName()
            .Version

        printfn $"{version.Major}.{version.Minor}.{version.Build}"
        return! Ok 0
      | [ List_Templates ] -> return Commands.runListTemplates ()
      | [ Restore ] -> return! Commands.runRestore ()
      | [ Remove_Template name ] -> return! Commands.runRemoveTemplate name
      | _ ->
        match parsed.TryGetSubCommand() with
        | Some (DevServerArgs.Build items) ->
          let buildConfig = Commands.getBuildOptions (items.GetAllResults())

          System.IO.Path.SetCurrentDirectoryToPerlaConfigDirectory()
          do! Commands.startBuild buildConfig :> Task
          return! Ok 0
        | Some (DevServerArgs.Serve items) ->
          System.IO.Path.SetCurrentDirectoryToPerlaConfigDirectory()

          do!
            Commands.startInteractive (fun () ->
              Commands.getServerOptions (items.GetAllResults()))

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
        | Some (New subcmd) ->
          return!
            subcmd
            |> NewProjectArgs.ToOptions
            |> Commands.runNew
        | Some (Add_Template subcmd) ->
          return!
            subcmd
            |> RepositoryArgs.ToOptions
            |> Commands.runAddTemplate None
        | Some (Update_Template subcmd) ->
          return!
            subcmd
            |> RepositoryArgs.ToOptions
            |> Commands.runUpdateTemplate
        | err ->
          parsed.Raise("No Commands Specified", showUsage = true)
          return! CommandNotParsedException $"%A{err}" |> Error
    with
    | ex -> return! ex |> Error
  }
  |> processExit
  |> Async.AwaitTask
  |> Async.RunSynchronously
