namespace Perla.Plugins.Registry

open System
open System.Collections.Generic
open System.IO
open System.Threading

open FSharp.Compiler.Interactive.Shell
open FSharp.Control
open IcedTasks
open FsToolkit.ErrorHandling
open Perla.Plugins

[<Struct>]
type PluginLoadError =
  | SessionExists
  | BoundValueMissing
  | AlreadyLoaded of string
  | EvaluationFailed of evalFailure: exn
  | NoPluginFound of pluginName: string

module SessionFactory =
  let inline create stdout stderr =
    FsiEvaluationSession.Create(
      FsiEvaluationSession.GetDefaultConfiguration(),
      [| "fsi.exe"; "--optimize+"; "--nologo"; "--gui-"; "--readline-" |],
      new StringReader(""),
      stdout,
      stderr,
      true
    )

module ScriptReflection =
  let inline findPlugin (fsi: FsiEvaluationSession) =
    match fsi.TryFindBoundValue "Plugin", fsi.TryFindBoundValue "plugin" with
    | Some bound, _ -> Some bound.Value
    | None, Some bound -> Some bound.Value
    | None, None -> None

type SessionCache<'Cache
  when 'Cache: (static member SessionCache:
    Lazy<Dictionary<string, FsiEvaluationSession>>)> = 'Cache

type PluginCache<'Cache
  when 'Cache: (static member PluginCache: Lazy<Dictionary<string, PluginInfo>>)>
  = 'Cache

type StdoutStderr<'Writer
  when 'Writer: (static member Stdout: TextWriter)
  and 'Writer: (static member Stderr: TextWriter)> = 'Writer


[<RequireQualifiedAccess>]
module PluginRegistry =

  let inline GetRunnablePlugins<'Cache when PluginCache<'Cache>> order = [
    for name in order do
      match 'Cache.PluginCache.Value.TryGetValue(name) with
      | true, plugin ->
        match plugin.shouldProcessFile, plugin.transform with
        | ValueSome st, ValueSome t -> {
            plugin = plugin
            shouldTransform = st
            transform = t
          }
        | _ -> ()
      | false, _ -> ()
  ]

  let inline GetPluginList<'Cache when PluginCache<'Cache>> () =
    'Cache.PluginCache.Value.Values |> Seq.toList

  let inline RunPlugins<'Cache when PluginCache<'Cache>>
    (pluginOrder: string list)
    fileInput
    =
    async {
      let plugins = GetRunnablePlugins<'Cache> pluginOrder

      let inline folder result next =
        cancellableValueTask {
          match next.shouldTransform result.extension with
          | true -> return! next.transform result
          | false -> return result
        }
        |> Async.AwaitCancellableValueTask

      return! plugins |> AsyncSeq.ofSeq |> AsyncSeq.foldAsync folder fileInput
    }

  let inline HasPluginsForExtension<'Cache when PluginCache<'Cache>> extension =
    let list = GetPluginList<'Cache>()

    list
    |> List.exists (fun plugin ->

      match plugin.shouldProcessFile with
      | ValueSome f -> f extension
      | _ -> false)

  let inline LoadFromCode<'Cache when PluginCache<'Cache>>
    (plugin: PluginInfo)
    =
    if 'Cache.PluginCache.Value.TryAdd(plugin.name, plugin) then
      Ok()
    else
      Error(AlreadyLoaded plugin.name)

[<Class; Sealed>]
type PluginRegistry =

  static member inline LoadFromText<'Cache, 'Writer
    when PluginCache<'Cache> and SessionCache<'Cache> and StdoutStderr<'Writer>>
    (
      id,
      content,
      cancellationToken
    ) =
    result {
      let mutable discard: FsiEvaluationSession = Unchecked.defaultof<_>
      let sessionCache = 'Cache.SessionCache

      do!
        sessionCache.Value.TryGetValue(id, &discard)
        |> Result.requireFalse SessionExists

      let plugins = 'Cache.PluginCache

      let session = SessionFactory.create 'Writer.Stdout 'Writer.Stderr

      let evaluation, _ =
        session.EvalInteractionNonThrowing(
          content,
          ?cancellationToken = cancellationToken
        )

      let! foundValue = result {
        let! result =
          evaluation |> Result.ofChoice |> Result.mapError EvaluationFailed

        match result with
        | Some result -> return result
        | None ->
          return!
            ScriptReflection.findPlugin session
            |> Result.requireSome BoundValueMissing
      }

      if foundValue.ReflectionType = typeof<PluginInfo> then
        sessionCache.Value.Add(id, session)
        let plugin: PluginInfo = unbox foundValue.ReflectionValue
        plugins.Value.Add(id, plugin)
        return plugin
      else
        return! Error(NoPluginFound id)
    }
