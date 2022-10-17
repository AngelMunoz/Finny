// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp
open FSharp.SystemCommandLine
open Perla
open Perla.Types
open Perla.CliOptions
open Perla.Commands

[<EntryPoint>]
let main argv =

  let port =
    Input.OptionMaybe<int>(
      [ "--port"; "-p" ],
      "Port where the application starts"
    )

  let host =
    Input.OptionMaybe<string>(
      [ "--host" ],
      "network ip address where the application will run"
    )

  let ssl = Input.OptionMaybe<bool>([ "--ssl" ], "Run dev server with SSL")


  let buildArgs
    (
      mode: string option,
      port: int option,
      host: string option,
      ssl: bool option
    ) : ServeOptions =
    { mode = mode |> Option.map RunConfiguration.FromString
      port = port
      host = host
      ssl = ssl }

  rootCommand argv {
    description "The Perla Dev Server!"
    inputs (modeArg, port, host, ssl)
    setHandler (buildArgs >> Handlers.runServe)

    addCommands
      [ buildCmd
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
