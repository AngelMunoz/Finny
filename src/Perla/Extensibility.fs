namespace Perla.Extensibility

open Perla
open Perla.Types
open Perla.FileSystem
open Perla.Plugins
open Perla.Plugins.Extensibility
open Perla.Esbuild

module Plugins =

  let PluginList () = Plugin.SupportedPlugins()

  let LoadPlugins (config: EsbuildConfig) =
    Esbuild.GetPlugin(config) |> Plugin.AddPlugin
    FileSystem.PluginFiles() |> Plugin.FromTextBatch

  let ApplyPlugins (content, extension) =
    Plugin.ApplyPluginsToFile(
      { content = content
        extension = extension }
    )
