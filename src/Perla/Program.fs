// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp
open FSharp.SystemCommandLine
open Perla
open Perla.Types
open Perla.CliOptions
open Perla.Commands
open System.Threading.Tasks

[<EntryPoint>]
let main argv =
  let maybeHelp = Input.OptionMaybe([ "--info" ], "Brings the Help dialog")

  let handler (help: bool option) = Task.FromResult 0

  rootCommand argv {
    description "The Perla Dev Server!"
    inputs maybeHelp
    setHandler (handler)

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
        newProjectCmd ]
  }
  |> Async.AwaitTask
  |> Async.RunSynchronously
