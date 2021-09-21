namespace Perla

open FSharp.Control.Tasks

open CliWrap

open Types
open System

module Fable =
  let mutable private activeFable: int option = None

  let private killActiveProcess pid =
    try
      let activeProcess =
        System.Diagnostics.Process.GetProcessById pid

      activeProcess.Kill()
    with
    | ex -> printfn $"Failed to Kill Procees with PID: [{pid}]\n{ex.Message}"

  let private addOutDir
    (outdir: string option)
    (args: Builders.ArgumentsBuilder)
    =
    match outdir with
    | Some outdir -> args.Add $"-o {outdir}"
    | None -> args

  let private addExtension
    (extension: string option)
    (args: Builders.ArgumentsBuilder)
    =
    match extension with
    | Some extension -> args.Add $"-e {extension}"
    | None -> args

  let private addWatch (watch: bool option) (args: Builders.ArgumentsBuilder) =
    match watch with
    | Some true -> args.Add $"watch"
    | Some false
    | None -> args

  let fableCmd (isWatch: bool option) =

    fun (config: FableConfig) ->
      let execBinName =
        if Env.isWindows then
          "dotnet.exe"
        else
          "dotnet"

      Cli
        .Wrap(execBinName)
        .WithArguments(fun args ->
          args
            .Add("fable")
            .Add(defaultArg config.project "./src/App.fsproj")
          |> addWatch isWatch
          |> addOutDir config.outDir
          |> addExtension config.extension
          |> ignore)
        .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
        .WithStandardOutputPipe(
          PipeTarget.ToStream(Console.OpenStandardOutput())
        )

  let stopFable () =
    match activeFable with
    | Some pid -> killActiveProcess pid
    | None -> printfn "No active Fable found"

  let startFable
    (getCommand: FableConfig option -> Command)
    (config: FableConfig option)
    =
    task {
      let cmdResult = getCommand(config).ExecuteAsync()
      activeFable <- Some cmdResult.ProcessId

      return! cmdResult.Task
    }
