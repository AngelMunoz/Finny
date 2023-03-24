// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp
open System.CommandLine.Invocation
open System.CommandLine.Help
open System.CommandLine.Builder
open FSharp.SystemCommandLine
open Perla
open System.Threading.Tasks

[<EntryPoint>]
let main argv =
  let maybeHelp = Input.OptionMaybe([ "--info" ], "Brings the Help dialog")

  let handler (_: InvocationContext, _: bool option) =

    Task.FromResult 0

  let serve, serveShorthand = Commands.Serve

  rootCommand argv {
    description "The Perla Dev Server!"
    inputs (Input.Context(), maybeHelp)
    setHandler handler

    usePipeline (fun pipeline ->
      // don't replace leading @ strings e.g. @lit-labs/task
      pipeline.UseTokenReplacer(fun _ _ _ -> false) |> ignore)

    addCommands
      [ serve
        serveShorthand
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
        Commands.NewProject
        Commands.Test
        Commands.Describe ]
  }
  |> Async.AwaitTask
  |> Async.RunSynchronously
