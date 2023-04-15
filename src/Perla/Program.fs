// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System.Threading.Tasks
open System.CommandLine.Invocation
open System.CommandLine.Builder

open FSharp.SystemCommandLine

open Perla
open Perla.Commands
open Perla.CliMiddleware

[<EntryPoint>]
let main argv =

  let maybeHelp = Input.OptionMaybe([ "--info" ], "Brings the Help dialog")

  let handler (_: InvocationContext, _: bool option) =

    Task.FromResult 0

  rootCommand argv {
    description "The Perla Dev Server!"
    inputs (Input.Context(), maybeHelp)
    setHandler handler

    usePipeline (fun pipeline ->
      pipeline
        // don't replace leading @ strings e.g. @lit-labs/task
        .UseTokenReplacer(fun _ _ _ -> false)
        .AddMiddleware(Middleware.SetupCheck)
        .AddMiddleware(Middleware.EsbuildBinCheck)
        .AddMiddleware(Middleware.TemplatesCheck)
        // Check if the esbuild plugin is present in PerlaConfiguration
        .AddMiddleware(Middleware.EsbuildPluginCheck)
        // Check for hidden commands and if the preview directive is enabled
        .AddMiddleware(Middleware.PreviewCheck)
      |> ignore)

    addCommands [
      Commands.Setup
      Commands.Template
      Commands.Describe
      Commands.Build
      Commands.Serve
      Commands.Test
      Commands.SearchPackage
      Commands.ShowPackage
      Commands.AddPackage
      Commands.RemovePackage
      Commands.ListPackages
      Commands.RestoreImportMap
      Commands.NewProject
    ]
  }
  |> Async.AwaitTask
  |> Async.RunSynchronously
