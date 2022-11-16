namespace Perla.Fable

open System
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices

open CliWrap

open Perla
open Perla.Types
open Perla.Units
open Perla.Logger

open FSharp.Control.Reactive
open FSharp.UMX

[<RequireQualifiedAccess>]
type FableEvent =
  | Log of string
  | ErrLog of string
  | WaitingForChanges

module Fable =

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
      stdout: Action<string>,
      stderr: Action<string>
    ) =
    let execBinName = if Env.IsWindows then "dotnet.exe" else "dotnet"

    Cli
      .Wrap(execBinName)
      .WithArguments(fun args ->
        args.Add("fable")
        |> addWatch isWatch
        |> addProject config.project
        |> addOutDir config.outDir
        |> addExtension config.extension
        |> ignore)
      .WithStandardErrorPipe(PipeTarget.ToDelegate(stderr))
      .WithStandardOutputPipe(PipeTarget.ToDelegate(stdout))

type Fable =

  static member Start
    (
      config: FableConfig,
      ?stdout: string -> unit,
      ?stderr: string -> unit,
      ?cancellationToken: CancellationToken
    ) =
    task {
      let stdout =
        let stdout = stdout |> Option.map (fun log -> Action<string>(log))
        defaultArg stdout (Action<string>(printfn "Fable: %s"))

      let stderr =
        let stderr = stderr |> Option.map (fun log -> Action<string>(log))
        defaultArg stderr (Action<string>(eprintfn "Fable: %s"))

      let cmdResult =
        Fable
          .fableCmd(config, false, stdout, stderr)
          .ExecuteAsync(?cancellationToken = cancellationToken)

      Logger.log $"Starting Fable with pid: [{cmdResult.ProcessId}]"

      return! cmdResult.Task
    }

  static member Observe
    (
      config: FableConfig,
      [<Optional>] ?isWatch: bool,
      [<Optional>] ?stdout: string -> unit,
      [<Optional>] ?stderr: string -> unit,
      [<Optional>] ?cancellationToken: CancellationToken
    ) : IObservable<FableEvent> =
    let sub = Subject.replay

    let EmitFableEvent (value: string) =
      if value.ToLowerInvariant().Contains("watching") then
        sub.OnNext(FableEvent.WaitingForChanges)
      else
        sub.OnNext(FableEvent.Log value)

    let stdout =
      match stdout with
      | None -> Action<string>(fun e -> EmitFableEvent e)
      | Some stdout ->
        Action.Combine(Action<string>(stdout), Action<string>(EmitFableEvent))
        :?> Action<string>

    let stderr =
      let stderr = stderr |> Option.map (fun stderr -> Action<string>(stderr))
      defaultArg stderr (Action<string>(eprintfn "%s"))

    let cmdResult =
      Fable.fableCmd (config, defaultArg isWatch true, stdout, stderr)

    async {
      try
        let cmdResult =
          cmdResult.ExecuteAsync(?cancellationToken = cancellationToken)

        Logger.log $"Starting Fable with pid: [{cmdResult.ProcessId}]"
        do! cmdResult.Task :> Task |> Async.AwaitTask
        sub.OnCompleted()
      with ex ->
        sub.OnError(ex)
    }
    |> Async.Start

    sub
