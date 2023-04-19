// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System.Threading.Tasks
open System.CommandLine.Invocation
open System.CommandLine.Builder

open FSharp.SystemCommandLine

open Perla
open Perla.FileSystem
open Perla.Database
open Perla.Configuration
open Perla.Handlers
open Perla.Commands
open Perla.CliMiddleware
open Perla.Types

[<RequireQualifiedAccess>]
module PerlaEnv =
  let setup =
    { new Setup with
        member _.IsAlreadySetUp() = Checks.IsSetupPresent()
        member _.SaveSetup() = Checks.SaveSetup()

        member _.RunSetup(isInCI, token) =
          Handlers.runSetup (
            {
              skipPrompts = isInCI
              installTemplates = not isInCI
            },
            token
          )
    }

  let esbuild (config: PerlaConfig) =
    { new Esbuild with
        member _.EsbuildVersion = config.esbuild.version

        member _.IsEsbuildPresent(version) = Checks.IsEsbuildBinPresent version

        member _.SaveEsbuildPresent version =
          Checks.SaveEsbuildBinPresent version

        member _.RunSetupEsbuild(version, token) =
          FileSystem.SetupEsbuild(version, token)

        member _.IsEsbuildPluginAbsent =
          config.plugins
          |> List.contains Constants.PerlaEsbuildPluginName
          |> not
    }

  let templates =
    { new Templates with
        member _.RunSetupTemplates
          (
            options: TemplateRepositoryOptions,
            token: System.Threading.CancellationToken
          ) : Task<int> =
          Handlers.runTemplate (options, token)

        member _.SaveTemplatesArePresent() : LiteDB.ObjectId =
          Checks.SaveTemplatesPresent()

        member _.TemplatesArePresent() : bool = Checks.AreTemplatesPresent()
    }

  let fable (config: PerlaConfig) =
    { new Fable with
        member _.IsFableInConfig = config.fable.IsSome
        member _.IsFablePresent token = FileSystem.CheckFableExists token
        member _.RestoreFable token = FileSystem.DotNetToolRestore token
    }

  let dotenv =
    { new DotEnv with
        member _.GetDotEnvFiles() = FileSystem.GetDotEnvFilePaths()
        member _.LoadDotEnvFiles paths = Env.LoadEnvFiles paths
    }


[<Struct>]
type AppEnv(config: PerlaConfig) =

  interface CliMiddlewareEnv with
    member _.DotEnv = PerlaEnv.dotenv
    member _.Esbuild = PerlaEnv.esbuild config
    member _.Fable = PerlaEnv.fable config
    member _.Setup = PerlaEnv.setup
    member _.Templates = PerlaEnv.templates


[<EntryPoint>]
let main argv =

  let maybeHelp = Input.OptionMaybe([ "--info" ], "Brings the Help dialog")

  let handler (_: InvocationContext, _: bool option) =

    Task.FromResult 0

  ConfigurationManager.UpdateFromFile()
  let perlaEnv = AppEnv(ConfigurationManager.CurrentConfig)


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
        .AddMiddleware(Middleware.SetupCheck perlaEnv)
        // Setup Esbuild in case it's not already setup
        .AddMiddleware(Middleware.EsbuildBinCheck perlaEnv)
        // Download templates if they're not already present
        .AddMiddleware(Middleware.TemplatesCheck perlaEnv)
        // Check if the esbuild plugin is present in PerlaConfiguration
        .AddMiddleware(Middleware.EsbuildPluginCheck perlaEnv)
        // Run Dotnet tool if fable is in config and not installed
        .AddMiddleware(Middleware.FableCheck perlaEnv)
        // Add .env files to the environment
        .AddMiddleware(Middleware.RunDotEnv perlaEnv)
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
