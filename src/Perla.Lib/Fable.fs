namespace Perla.Lib

open CliWrap

open System
open Types
open Logger

module Fable =
  let mutable private activeFable: int option = None

  let private killActiveProcess pid =
    try
      let activeProcess = System.Diagnostics.Process.GetProcessById pid

      activeProcess.Kill()
    with
    | ex -> Logger.log ($"Failed to kill process with PID: [{pid}]", ex)

  let private addProject
    (project: string option)
    (args: Builders.ArgumentsBuilder)
    =
    let project = defaultArg project "./src/App.fsproj"
    args.Add project

  let private addOutDir
    (outdir: string option)
    (args: Builders.ArgumentsBuilder)
    =
    match outdir with
    | Some outdir -> args.Add([ "-o"; $"{outdir}" ])
    | None -> args

  let private addExtension
    (extension: string option)
    (args: Builders.ArgumentsBuilder)
    =
    match extension with
    | Some extension -> args.Add([ "-e"; $"{extension}" ])
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
        .WithEnvironmentVariables(fun env -> env.Set("CI", "1") |> ignore)
        .WithArguments(fun args ->
          args.Add("fable")
          |> addWatch isWatch
          |> addProject config.project
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
    | None -> Logger.log "No active Fable found"

  let startFable
    (getCommand: FableConfig option -> Command)
    (config: FableConfig option)
    =
    task {
      let cmdResult = getCommand(config).ExecuteAsync()
      activeFable <- Some cmdResult.ProcessId

      return! cmdResult.Task
    }
