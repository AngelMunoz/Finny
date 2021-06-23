namespace FSharp.DevServer

open System.Runtime.InteropServices

open FSharp.Control.Tasks

open CliWrap

open Types
open System

module Fable =
  let mutable private activeFable : int option = None

  let private killActiveProcess pid =
    try
      let activeProcess =
        System.Diagnostics.Process.GetProcessById pid

      activeProcess.Kill()
    with
    | ex -> printfn $"Failed to Kill Procees with PID: [{pid}]\n{ex.Message}"

  let fableCmd (isWatch: bool option) =
    let isWatch = defaultArg isWatch false

    fun (config: FableConfig) ->
      let project =
        defaultArg config.Project "./src/App.fsproj"

      let outDir = defaultArg config.OutDir "./public"
      let extension = defaultArg config.Extension ".fs.js"

      let isWindows =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)

      let watch =
        $"""{if isWatch then " watch " else " "}"""

      Cli
        .Wrap(if isWindows then "dotnet.exe" else "")
        .WithArguments($"fable{watch}{project} -o {outDir} -e {extension}")
        .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
        .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))

  let stopFable () =
    match activeFable with
    | Some pid -> killActiveProcess pid
    | None -> printfn "No active Fable found"

  let startFable (getCommand: FableConfig -> Command) (config: FableConfig) =
    task {
      let cmdResult = getCommand(config).ExecuteAsync()
      activeFable <- Some cmdResult.ProcessId

      return! cmdResult.Task
    }
