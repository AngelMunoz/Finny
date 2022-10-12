// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp
open System.Threading.Tasks

open System.CommandLine
open FSharp.SystemCommandLine
open Perla.Commands

[<EntryPoint>]
let main argv =
  let isInteractive =
    Input.OptionMaybe(
      [ "-i"; "--interactive" ],
      "Starts the server in interactive mode"
    )

  let rootHandler (isInteractive: bool option) = task { return 0 }

  rootCommand argv {
    description "The Perla Dev Server!"
    inputs isInteractive
    setHandler rootHandler

    addCommands
      [ serveCmd
        buildCmd
        initCmd
        searchPackagesCmd
        showPackageCmd
        removePackageCmd
        addPacakgeCmd
        listCmd
        restoreCmd
        addTemplateCmd
        updateTemplateCmd
        listTemplatesCmd
        removeTemplateCmd
        newProjectCmd
        versionCmd ]
  }
  |> Async.AwaitTask
  |> Async.RunSynchronously
