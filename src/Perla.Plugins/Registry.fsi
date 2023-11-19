namespace Perla.Plugins.Registry

open System
open System.IO
open System.Text
open System.Threading

open IcedTasks

open Perla.Plugins

type SessionManager =

  interface IDisposable

  private new: unit -> SessionManager
  private new: stdout: TextWriter * stderr: TextWriter -> SessionManager

  static member Create: unit -> SessionManager

  static member Create:
    stdout: StringWriter * stderr: StringWriter -> SessionManager

  static member Create:
    stdout: StringBuilder * stderr: StringBuilder -> SessionManager

  member LoadFromText:
    id: string * content: string * ?token: CancellationToken ->
      PluginInfo voption


type PluginRegistry =

  private new: unit -> PluginRegistry

  member TryRegister: PluginInfo -> bool

  member GetRunnables: order: string seq -> RunnablePlugin list


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

  val inline ofTextCancellable<'PluginRegistry, 'SessionManager
    when RegisterPlugin<'PluginRegistry> and FromText<'SessionManager>> :
    registry: 'PluginRegistry * sessionManager: 'SessionManager ->
      id: string * content: string * token: CancellationToken option ->
        PluginInfo voption

  val inline ofText<'PluginRegistry, 'SessionManager
    when RegisterPlugin<'PluginRegistry> and FromText<'SessionManager>> :
    registry: 'PluginRegistry * sessionManager: 'SessionManager ->
      id: string * content: string ->
        PluginInfo voption

  val inline runPlugins<'PluginRegistry when GetRunnables<'PluginRegistry>> :
    registry: 'PluginRegistry ->
    pluginOrder: string list * fileInput: FileTransform ->
      CancellableTask<FileTransform>
