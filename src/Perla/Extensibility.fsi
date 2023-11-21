namespace Perla.Extensibility

open System.IO
open System.Collections.Generic
open FSharp.Compiler.Interactive.Shell

open FsToolkit.ErrorHandling

open Perla.Types
open Perla.Plugins
open Perla.Plugins.Registry

[<Struct>]
type ExtCache =
  static member PluginCache: Lazy<Dictionary<string, PluginInfo>>

  static member SessionCache: Lazy<Dictionary<string, FsiEvaluationSession>>

[<Struct>]
type PluginStdio =

  static member Stdout: TextWriter

  static member Stderr: TextWriter

[<RequireQualifiedAccess>]
module PluginLoader =

  val inline Load<'Fs, 'Esbuild
    when 'Fs: (static member PluginFiles: unit -> (string * string) array)
    and 'Esbuild: (static member GetPlugin: EsbuildConfig -> PluginInfo)> :
    esbuildConfig: EsbuildConfig -> Validation<PluginInfo list, PluginLoadError>
