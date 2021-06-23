namespace FSharp.DevServer


open System
open FSharp.Control
open FSharp.Control.Tasks

open Types
open Server
open Build

module Commands =

  let startInteractive (configuration: DevServerConfig * FableConfig) =
    let devConfig, fableConfig = configuration
    let onStdinAsync = serverActions devConfig fableConfig

    let autoStartServer = defaultArg devConfig.AutoStart true
    let autoStartFable = defaultArg fableConfig.AutoStart true

    asyncSeq {
      if autoStartServer then "start"
      if autoStartFable then "start:fable"

      while true do
        let! value = Console.In.ReadLineAsync() |> Async.AwaitTask
        value
    }
    |> AsyncSeq.distinctUntilChanged
    |> AsyncSeq.iterAsync onStdinAsync

  let startBuild (configuration: BuildConfig * FableConfig) =
    let buildConfig, fableConfig = configuration
    execBuild buildConfig fableConfig
