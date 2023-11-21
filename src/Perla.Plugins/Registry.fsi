namespace Perla.Plugins.Registry

open System.Collections.Generic
open System.IO
open System.Threading

open FSharp.Compiler.Interactive.Shell

open IcedTasks

open Perla.Plugins

[<Struct>]
type PluginLoadError =
  | SessionExists
  | BoundValueMissing
  | EvaluationFailed of evalFailure: exn
  | NoPluginFound of pluginName: string

type SessionCache<'Cache
  when 'Cache: (static member SessionCache:
    Dictionary<string, FsiEvaluationSession>)> = 'Cache

type PluginCache<'Cache
  when 'Cache: (static member PluginCache: Dictionary<string, PluginInfo>)> =
  'Cache

type StdoutStderr<'Writer
  when 'Writer: (static member Stdout: TextWriter)
  and 'Writer: (static member Stderr: TextWriter)> = 'Writer


[<RequireQualifiedAccess>]
module PluginRegistry =
  val inline GetRunnablePlugins<'Cache when PluginCache<'Cache>> :
    string seq -> RunnablePlugin list

  val inline GetPluginList<'Cache when PluginCache<'Cache>> :
    unit -> PluginInfo list

  val inline RunPlugins<'Cache when PluginCache<'Cache>> :
    pluginOrder: string list -> fileInput: FileTransform -> Async<FileTransform>

  val inline HasPluginsForExtension<'Cache when PluginCache<'Cache>> :
    extension: string -> bool

[<Class; Sealed>]
type PluginRegistry =

  static member inline LoadFromText<'Cache, 'Writer
    when PluginCache<'Cache> and SessionCache<'Cache> and StdoutStderr<'Writer>> :
    id: string * content: string * ?cancellationToken: CancellationToken ->
      Result<PluginInfo, PluginLoadError>
