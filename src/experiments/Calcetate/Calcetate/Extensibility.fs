module Calcetate.Extensibility

open System
open System.Collections.Concurrent
open System.IO
open System.Threading.Tasks
open AngleSharp.Html
open CalceTypes
open System.Text
open FSharp.Control
open Perla.Lib.Logger
open FsToolkit.ErrorHandling

open FSharp.Compiler.Interactive.Shell
open Spectre.Console

[<Struct>]
type RunnablePlugin =
  { plugin: PluginInfo
    shouldTransform: FilePredicate
    transform: TransformAction }

let PluginCache =
  ConcurrentDictionary<string, PluginInfo * FsiEvaluationSession>()

let private getSession stdin stdout stderr =
  let defConfig = FsiEvaluationSession.GetDefaultConfiguration()

  let argv = [| "fsi.exe"; "--optimize+"; "--nologo"; "--gui-"; "--readline-" |]

  FsiEvaluationSession.Create(defConfig, argv, stdin, stdout, stderr, true)

let LoadPluginFromScript
  (
    name: string,
    content: string option
  ) : PluginInfo option =
  match PluginCache.TryGetValue name with
  | (true, (plugin, _)) ->
    Logger.log $"Loading plugin [{plugin.name}] from cache"
    Some plugin
  | (false, _) ->
    Logger.log $"Plugin [{name}] not in cache, loading from content string"

    let Fsi = getSession (new StringReader("")) Console.Out Console.Error

    let content = defaultArg content ""

    (match Fsi.EvalInteractionNonThrowing(content) with
     | Choice1Of2 (Some value), _ ->
       match value.ReflectionType = typeof<PluginInfo> with
       | true -> unbox value.ReflectionValue |> Some
       | false -> None
     | Choice1Of2 None, diag ->
       printfn "%A" diag
       None
     | Choice2Of2 (ex), diag ->
       eprintfn "%O" ex
       printfn "%A" diag
       None)
    |> Option.orElseWith (fun _ ->
      let bound =
        Fsi.TryFindBoundValue "Plugin"
        |> Option.orElseWith (fun _ -> Fsi.TryFindBoundValue "plugin")

      match bound with
      | Some bound ->
        match bound.Value.ReflectionType = typeof<PluginInfo> with
        | true -> unbox bound.Value.ReflectionValue |> Some
        | false -> None
      | None -> None)
    |> Option.map (fun value ->
      match PluginCache.TryAdd(value.name, (value, Fsi)) with
      | true -> printfn $"Plugin [{value.name}] added to cache"
      | false -> printfn $"We couldn't add plugin [{value.name}] to cache"

      value)


let ApplyPluginsToFile (plugins: RunnablePlugin list) fileTransform =
  let folder result next =
    async {
      match next.shouldTransform result with
      | true -> return! (next.transform result).AsTask() |> Async.AwaitTask
      | false -> return result
    }
  plugins |> AsyncSeq.ofSeq |> AsyncSeq.foldAsync folder fileTransform

let GetSupportedPlugins plugins =
  let chooser (plugin: PluginInfo) =
    match plugin.shouldProcessFile, plugin.transform with
    | ValueSome st, ValueSome t ->
      Some
        { plugin = plugin
          shouldTransform = st
          transform = t }
    | _ -> None

  plugins |> List.choose chooser
