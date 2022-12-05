namespace Perla

open Perla
open Perla.Types
open Perla.FileSystem
open Perla.Plugins
open Perla.Plugins.Extensibility
open Perla.Esbuild

module Plugins =

  let PluginList (pluginOrder: string list) =
    let plugins =
      [ for plugin in pluginOrder do
          match Plugin.TryGetPluginByName plugin with
          | Some plugin -> plugin
          | None -> () ]

    Plugin.SupportedPlugins(plugins)

  let LoadPlugins (config: EsbuildConfig) =
    Esbuild.GetPlugin(config) |> Plugin.AddPlugin
    FileSystem.PluginFiles() |> Plugin.FromTextBatch

  let HasPluginsForExtension = Plugin.HasPluginsForExtension

  let ApplyPlugins (content, extension) =
    Plugin.ApplyPluginsToFile(
      { content = content
        extension = extension }
    )
