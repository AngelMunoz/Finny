// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp
open System.CommandLine.Invocation
open FSharp.SystemCommandLine
open Perla
open System.Threading.Tasks

[<EntryPoint>]
let main argv =
  let maybeHelp = Input.OptionMaybe([ "--info" ], "Brings the Help dialog")

  let handler (_: InvocationContext, _: bool option) =

    Task.FromResult 0

  rootCommand argv {
    description "The Perla Dev Server!"
    inputs (Input.Context(), maybeHelp)
    setHandler handler

    addCommands
      [ Commands.Serve
        Commands.Build
        Commands.Init
        Commands.SearchPackages
        Commands.ShowPackage
        Commands.RemovePackage
        Commands.AddPackage
        Commands.ListDependencies
        Commands.Restore
        Commands.AddTemplate
        Commands.UpdateTemplate
        Commands.ListTemplates
        Commands.RemoveTemplate
        Commands.NewProject ]
  }
  |> Async.AwaitTask
  |> Async.RunSynchronously
