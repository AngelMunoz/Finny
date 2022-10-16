namespace Perla.Extensibility

open FSharp.Control

open Perla
open Perla.Types
open Perla.FileSystem
open Perla.Plugins
open Perla.Plugins.Extensibility



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
