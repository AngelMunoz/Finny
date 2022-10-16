namespace Perla.Plugins.Extensibility

open System
open System.Collections.Concurrent
open System.IO
open System.Runtime.InteropServices

open FSharp.Compiler.Interactive.Shell
open FSharp.Control

open FsToolkit.ErrorHandling

open Perla.Plugins
open Perla.Logger
open System.Collections.Generic

type Fsi =
  static member GetSession
    (
      [<Optional>] ?argv: string seq,
      [<Optional>] ?stdout,
      [<Optional>] ?stderr
    ) =
    let defaultArgv =
      [| "fsi.exe"; "--optinize+"; "--nologo"; "--gui-"; "--readline-" |]

    let argv =
      match argv with
      | Some argv -> [| yield! defaultArgv; yield! argv |]
      | None -> defaultArgv

    let stdout = defaultArg stdout Console.Out
    let stderr = defaultArg stderr Console.Error

    let config = FsiEvaluationSession.GetDefaultConfiguration()

    FsiEvaluationSession.Create(
      config,
      argv,
      new StringReader(""),
      stdout,
      stderr,
      true
    )

type Plugin =

  static member FsiSessions =
    lazy(Dictionary<string, FsiEvaluationSession option>())

  static member PluginCache =
    lazy (Dictionary<string, PluginInfo>())

  static member CachedPlugins() =
    Plugin.PluginCache.Value |> Seq.map (fun entry -> entry.Value)

  static member FromText
    (
      path: string,
      content: string,
      [<Optional>] ?skipCache
    ) =
    let skipCache = defaultArg skipCache false
    Logger.log $"Extracting plugin '{path}' from text"

    let Fsi = Fsi.GetSession()

    let evaluation, _ = Fsi.EvalInteractionNonThrowing(content)

    let tryFindBoundValue () =
      match Fsi.TryFindBoundValue "Plugin", Fsi.TryFindBoundValue "plugin" with
      | Some bound, _ -> Some bound.Value
      | None, Some bound -> Some bound.Value
      | None, None -> None

    let logError ex =
      Logger.log ($"An error ocurrer while processing {path}", ex)

    let evaluation =
      evaluation
      |> Result.ofChoice
      |> Result.teeError logError
      |> Result.toOption
      |> Option.defaultWith tryFindBoundValue

    match evaluation with
    | Some value when value.ReflectionType = typeof<PluginInfo> ->
      let plugin = unbox plugin

      if not skipCache then
        match Plugin.PluginCache.Value.TryAdd(plugin.name, plugin) with
        | true ->
          Plugin.FsiSessions.Value.Add(plugin.name, Some Fsi)
          Logger.log $"Added %s{plugin.name} to plugin cache"
        | false ->
          Logger.log $"Couldn't add %s{plugin.name}"
      Some plugin
    | _ -> None

  static member FromTextBatch(plugins: (string * string) seq) =
    for (path, content) in plugins do
      Plugin.FromText(path, content) |> ignore

  static member AddPlugin(plugin: PluginInfo) =
    match Plugin.PluginCache.Value.TryAdd(plugin.name, plugin) with
    | true ->
      Plugin.FsiSessions.Value.Add(plugin.name, None)
      Logger.log $"Added %s{plugin.name} to plugin cache"
    | false -> Logger.log $"Couldn't add %s{plugin.name}"

  static member SupportedPlugins([<Optional>] ?plugins: PluginInfo seq): RunnablePlugin seq =
    let plugins = defaultArg plugins (Plugin.CachedPlugins())

    let chooser (plugin: PluginInfo) =
      match plugin.shouldProcessFile, plugin.transform with
      | ValueSome st, ValueSome t ->
        Some
          { plugin = plugin
            shouldTransform = st
            transform = t }
      | _ -> None

    plugins |> Seq.choose chooser

  static member ApplyPluginsToFile
    (
      fileInput: FileTransform,
      ?plugins: RunnablePlugin seq
    ) =
    let plugins = defaultArg plugins (Plugin.SupportedPlugins())

    let folder result next =
      async {
        match next.shouldTransform result with
        | true -> return! (next.transform result).AsTask() |> Async.AwaitTask
        | false -> return result
      }

    plugins |> AsyncSeq.ofSeq |> AsyncSeq.foldAsync folder fileInput

module Scaffolding =
  [<Literal>]
  let ScaffoldConfiguration = "TemplateConfiguration"

  let getConfigurationFromScript content =
    use session = Fsi.GetSession()

    session.EvalInteractionNonThrowing(content) |> ignore

    match session.TryFindBoundValue ScaffoldConfiguration with
    | Some bound -> Some bound.Value.ReflectionValue
    | None -> None
