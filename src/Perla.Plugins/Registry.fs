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
    | Some bound, _ -> ValueSome bound.Value
    | None, Some bound -> ValueSome bound.Value
    | None, None -> ValueNone

type SessionManager private () =
  let stdout = Unchecked.defaultof<_>
  let stderr = Unchecked.defaultof<_>

  let cache = Dictionary<string, FsiEvaluationSession>()

  private new(stdout, stderr: TextWriter) = new SessionManager(stdout, stderr)


  static member Create() =
    new SessionManager(Console.Out, Console.Error)

  static member Create(stdout: StringWriter, stderr: StringWriter) =
    new SessionManager(stdout, stderr)

  static member Create(stdout, stderr) =
    new SessionManager(
      new StringWriter(stdout, Globalization.CultureInfo.InvariantCulture),
      new StringWriter(stderr, Globalization.CultureInfo.InvariantCulture)
    )

  member _.LoadFromText(id, content, ?token) =
    match cache.TryGetValue(id) with
    | true, _ -> ValueNone
    | false, _ ->
      let session = SessionFactory.create stdout stderr

      let evaluation, _ =
        session.EvalInteractionNonThrowing(content, ?cancellationToken = token)

      let evalResult =
        evaluation
        |> Result.ofChoice
        |> Result.map (fun value -> ValueOption.ofOption value)
        |> Result.teeError (fun err -> stdout.WriteLine(err))
        |> Result.toValueOption
        |> ValueOption.defaultWith (fun _ ->
          ScriptReflection.findPlugin session)

      match evalResult with
      | ValueSome value when value.ReflectionType = typeof<PluginInfo> ->
        cache.Add(id, session)

        let plugin: PluginInfo = unbox value.ReflectionValue
        cache.Add(id, session)

        ValueSome plugin
      | _ -> ValueNone

  interface IDisposable with
    member _.Dispose() =
      for session in cache.Values do
        (session :> IDisposable).Dispose()

type PluginRegistry() =

  let plugins = Dictionary<string, PluginInfo>()

  member _.TryRegister plugin = plugins.TryAdd(plugin.name, plugin)

  member _.GetRunnables order = [
    for name in order do
      match plugins.TryGetValue(name) with
      | true, plugin ->
        match plugin.shouldProcessFile, plugin.transform with
        | ValueSome st, ValueSome t ->
          yield {
            plugin = plugin
            shouldTransform = st
            transform = t
          }
        | _ -> ()
      | false, _ -> ()
  ]


[<RequireQualifiedAccess>]
module PluginRegistry =

  type GetRunnables<'Registry
    when 'Registry: (member GetRunnables: string list -> RunnablePlugin list)> =
    'Registry

  type RegisterPlugin<'Registry
    when 'Registry: (member TryRegister: PluginInfo -> bool)> = 'Registry

  type FromText<'SessionManager
    when 'SessionManager: (member LoadFromText:
      string * string * CancellationToken option -> PluginInfo voption)> =
    'SessionManager

  let inline ofTextCancellable<'PluginRegistry, 'SessionManager
    when RegisterPlugin<'PluginRegistry> and FromText<'SessionManager>>
    (registry: 'PluginRegistry, sessionManager: 'SessionManager)
    (id: string, content: string, token: CancellationToken option)
    =
    voption {
      let! plugin = sessionManager.LoadFromText(id, content, token)

      match registry.TryRegister(plugin) with
      | true -> return plugin
      | false -> return! ValueNone
    }

  let inline ofText
    (registry: 'PluginRegistry, sessionManager: 'SessionManager)
    (id, content)
    =
    ofTextCancellable (registry, sessionManager) (id, content, None)

  let inline runPlugins<'PluginRegistry when GetRunnables<'PluginRegistry>>
    (registry: 'PluginRegistry)
    (pluginOrder, fileInput)
    =
    cancellableTask {
      let plugins = registry.GetRunnables pluginOrder

      let inline folder result next =
        cancellableValueTask {
          match next.shouldTransform result.extension with
          | true -> return! next.transform result
          | false -> return result
        }
        |> Async.AwaitCancellableValueTask

      return! plugins |> AsyncSeq.ofSeq |> AsyncSeq.foldAsync folder fileInput
    }
