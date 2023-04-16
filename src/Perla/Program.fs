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
        // Check for hidden commands and if the preview directive is enabled
        .AddMiddleware(Middleware.PreviewCheck)
        // Setup Perla if it's not already setup
        .AddMiddleware(Middleware.SetupCheck)
        // Setup Esbuild in case it's not already setup
        .AddMiddleware(Middleware.EsbuildBinCheck)
        // Download templates if they're not already present
        .AddMiddleware(Middleware.TemplatesCheck)
        // Check if the esbuild plugin is present in PerlaConfiguration
        .AddMiddleware(Middleware.EsbuildPluginCheck)
        // Run Dotnet tool if fable is in config and not installed
        .AddMiddleware(Middleware.FableCheck)
        // Add .env files to the environment
        .AddMiddleware(Middleware.RunDotEnv)
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
