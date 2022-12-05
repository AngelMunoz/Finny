namespace Perla

open Perla.Types
open Perla.Plugins

module Plugins =
    val PluginList: pluginOrder: string list -> seq<RunnablePlugin>
    val LoadPlugins: config: EsbuildConfig -> unit
    val HasPluginsForExtension: string -> bool
    val ApplyPlugins: content: string * extension: string -> Async<FileTransform>
