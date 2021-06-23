// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open Argu

type ServerArgs =
  | Port of int option
  | Host of string option
  | Public of string option

  interface IArgParserTemplate with
    member this.Usage =
      match this with
      | Port _ -> "Select the server port, defaults to 7331"
      | Host _ -> "Server host, defaults to localhost"
      | Public _ -> "Path to the static files directory, defaults to './public'"

type BuildArgs =
  | Config of string
  interface IArgParserTemplate with
    member this.Usage =
      match this with
      | Config _ -> "Path to the configuration file"

type DevServerArgs =
  | [<CliPrefix(CliPrefix.None)>] Server of ParseResults<ServerArgs>
  | [<CliPrefix(CliPrefix.None)>] Build of ParseResults<BuildArgs>
  interface IArgParserTemplate with
    member this.Usage =
      match this with
      | Server _ ->
        "Starts a development server for modern Javascript development"
      | Build _ -> "Builds the specified JS and CSS resources for production"

[<EntryPoint>]
let main argv =
  let parser = ArgumentParser.Create<DevServerArgs>()

  try
    let results =
      parser.ParseCommandLine(
        inputs = argv,
        raiseOnUsage = true,
        ignoreMissing = true,
        ignoreUnrecognized = true
      )

    printfn "%A" results
  with
  | e -> printfn "%s" e.Message

  0 // return an integer exit code
