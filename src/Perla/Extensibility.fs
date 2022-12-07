namespace Perla.Extensibility

open System
open System.Collections.Concurrent
open System.IO
open System.Runtime.InteropServices

open FSharp.Compiler.Interactive.Shell
open FSharp.Control

open FsToolkit.ErrorHandling


open Perla
open Perla.Types
open Perla.FileSystem
open Perla.Plugins
open Perla.Esbuild
open Perla.Plugins
open Perla.Logger


type Fsi =
  static member GetSession
    (
      [<Optional>] ?argv: string seq,
      [<Optional>] ?stdout,
      [<Optional>] ?stderr
    ) =
    let defaultArgv =
      [| "fsi.exe"; "--optimize+"; "--nologo"; "--gui-"; "--readline-" |]

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


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PluginRegistry =

  let FsiSessions =
    lazy (ConcurrentDictionary<string, FsiEvaluationSession option>())

  let PluginCache = ConcurrentDictionary<string, PluginInfo>()

  let AddPlugin (plugin: PluginInfo) =
    match PluginCache.TryAdd(plugin.name, plugin) with
    | true ->
      FsiSessions.Value.TryAdd(plugin.name, None) |> ignore
      Logger.log $"Added %s{plugin.name} to plugin cache"
    | false -> Logger.log $"Couldn't add %s{plugin.name}"

  let FromText (path: string, content: string) =
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
      let plugin = unbox value.ReflectionValue

      match PluginCache.TryAdd(plugin.name, plugin) with
      | true -> Logger.log $"Added %s{plugin.name} to plugin cache"
      | false -> Logger.log $"Couldn't add %s{plugin.name}"

      Some plugin
    | _ -> None

  let CachedPlugins () =
    PluginCache |> Seq.map (fun entry -> entry.Value)

  let TryGetPluginByName name =
    match PluginCache.TryGetValue name with
    | true, plugin -> Some plugin
    | false, _ -> None


  let PluginList (pluginOrder: string list) =
    let plugins =
      [ for plugin in pluginOrder do
          match TryGetPluginByName plugin with
          | Some plugin -> plugin
          | None -> () ]

    let chooser (plugin: PluginInfo) =
      match plugin.shouldProcessFile, plugin.transform with
      | ValueSome st, ValueSome t ->
        Some
          { plugin = plugin
            shouldTransform = st
            transform = t }
      | _ -> None

    plugins |> Seq.choose chooser

  let LoadPlugins (config: EsbuildConfig) =
    Esbuild.GetPlugin(config) |> AddPlugin

    for (path, content) in FileSystem.PluginFiles() do
      FromText(path, content) |> ignore

  let HasPluginsForExtension plugins extension =
    PluginList plugins
    |> Seq.exists (fun runnable -> runnable.shouldTransform extension)

  let ApplyPlugins (plugins: string list) (fileInput: FileTransform) =
    let plugins = PluginList(plugins)

    let folder result next =
      async {
        match next.shouldTransform result.extension with
        | true -> return! (next.transform result).AsTask() |> Async.AwaitTask
        | false -> return result
      }

    plugins |> AsyncSeq.ofSeq |> AsyncSeq.foldAsync folder fileInput



[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Scaffolding =
  [<Literal>]
  let ScaffoldConfiguration = "TemplateConfiguration"

  let getConfigurationFromScript content =
    use session = Fsi.GetSession()

    session.EvalInteractionNonThrowing(content) |> ignore

    match session.TryFindBoundValue ScaffoldConfiguration with
    | Some bound -> Some bound.Value.ReflectionValue
    | None -> None
