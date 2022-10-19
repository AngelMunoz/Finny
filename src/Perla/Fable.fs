namespace Perla.Fable

open CliWrap

open Perla
open Perla.Types
open Perla.Units
open Perla.Logger

open FSharp.UMX
open System.Threading

module Fable =
  let mutable activeFable: int option = None

  let killActiveProcess pid =
    try
      let activeProcess = System.Diagnostics.Process.GetProcessById pid

      activeProcess.Kill()
    with ex ->
      Logger.log ($"Failed to kill process with PID: [{pid}]", ex)

  let addProject
    (project: string<SystemPath>)
    (args: Builders.ArgumentsBuilder)
    =
    args.Add $"{project}"

  let addOutDir
    (outdir: string<SystemPath> option)
    (args: Builders.ArgumentsBuilder)
    =
    match outdir with
    | Some outdir -> args.Add([ "-o"; $"{outdir}" ])
    | None -> args

  let addExtension
    (extension: string<FileExtension>)
    (args: Builders.ArgumentsBuilder)
    =
    args.Add([ "-e"; $"{extension}" ])

  let addWatch (watch: bool) (args: Builders.ArgumentsBuilder) =
    if watch then args.Add $"watch" else args

  let fableCmd
    (
      config: FableConfig,
      isWatch: bool,
      stdout: (string -> unit),
      stderr: (string -> unit)
    ) =
    let execBinName = if Env.IsWindows then "dotnet.exe" else "dotnet"

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
      .WithStandardErrorPipe(PipeTarget.ToDelegate(stdout))
      .WithStandardOutputPipe(PipeTarget.ToDelegate(stderr))

type Fable =
  static member FablePid = Fable.activeFable

  static member Stop() =
    match Fable.activeFable with
    | Some pid -> Fable.killActiveProcess pid
    | None -> Logger.log "No active Fable found"

  static member Start
    (
      config: FableConfig,
      isWatch: bool,
      ?stdout: string -> unit,
      ?stderr: string -> unit,
      ?cancellationToken: CancellationToken
    ) =
    task {
      let stdout = defaultArg stdout (printfn "Fable: %s")
      let stderr = defaultArg stderr (eprintfn "Fable: %s")

      let cmdResult =
        Fable
          .fableCmd(config, isWatch, stdout, stderr)
          .ExecuteAsync(?cancellationToken = cancellationToken)

      Fable.activeFable <- Some cmdResult.ProcessId

      Logger.log $"Starting Fable with pid: [{cmdResult.ProcessId}]"

      return! cmdResult.Task
    }
