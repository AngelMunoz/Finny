namespace Perla.Extensibility

open System
open System.Collections.Generic
open FSharp.Compiler.Interactive.Shell

open FsToolkit.ErrorHandling

open Perla.Types
open Perla.Esbuild

open Perla.Plugins
open Perla.Plugins.Registry

[<Struct>]
type ExtCache =
  static let _pluginCache = lazy (Dictionary<string, PluginInfo>())

  static let _sessionCache = lazy (Dictionary<string, FsiEvaluationSession>())

  static member PluginCache = _pluginCache

  static member SessionCache = _sessionCache

[<Struct>]
type PluginStdio =

  static member Stdout = Console.Out

  static member Stderr = Console.Error

module PluginLoader =
  let inline Load<'Fs, 'Esbuild
    when 'Fs: (static member PluginFiles: unit -> (string * string) array)
    and 'Esbuild: (static member GetPlugin: EsbuildConfig -> PluginInfo)>
    (esbuildConfig: EsbuildConfig)
    =
    validation {
      let esbuild = 'Esbuild.GetPlugin esbuildConfig
      do! PluginRegistry.LoadFromCode<ExtCache> esbuild

      do!
        'Fs.PluginFiles()
        |> Array.Parallel.map (fun (path, content) ->
          PluginRegistry.LoadFromText<ExtCache, PluginStdio>(path, content))
        |> List.ofArray
        |> List.traverseResultA (fun plugin -> result {
          let! plugin = plugin
          Console.WriteLine $"Loaded plugin: {plugin.name}"

          do!
            ExtCache.PluginCache.Value.TryAdd(plugin.name, plugin)
            |> Result.requireTrue (AlreadyLoaded plugin.name)

          return plugin
        })
        |> Result.ignore

      return PluginRegistry.GetPluginList<ExtCache>()
    }
