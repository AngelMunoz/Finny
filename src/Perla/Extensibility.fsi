namespace Perla.Extensibility

open Perla
open Perla.Types
open Perla.FileSystem
open Perla.Plugins
open Perla.Plugins.Extensibility
open Perla.Esbuild

module Plugins =
    val PluginList: unit -> seq<RunnablePlugin>
    val LoadPlugins: config: EsbuildConfig -> unit
    val HasPluginsForExtension: string -> bool
    val ApplyPlugins: content: string * extension: string -> Async<FileTransform>
