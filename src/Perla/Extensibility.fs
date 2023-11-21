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
  static member PluginCache = new Dictionary<string, PluginInfo>()

  static member SessionCache = new Dictionary<string, FsiEvaluationSession>()

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
    result {
      let! plugins =
        'Fs.PluginFiles()
        |> Array.Parallel.map (fun (path, content) ->
          PluginRegistry.LoadFromText<ExtCache, PluginStdio>(path, content))
        |> List.ofArray
        |> List.traverseResultM id

      return 'Esbuild.GetPlugin esbuildConfig :: plugins
    }
