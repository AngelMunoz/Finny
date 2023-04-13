namespace Perla.Extensibility

open Perla.Types
open Perla.Plugins
open System.IO
open FSharp.Compiler.Interactive.Shell
open System.Runtime.InteropServices

[<Class>]
type Fsi =
  static member GetSession:
    [<Optional>] ?argv: string seq *
    [<Optional>] ?stdout: TextWriter *
    [<Optional>] ?stderr: TextWriter ->
      FsiEvaluationSession

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PluginRegistry =
  val PluginList: pluginOrder: string list -> seq<RunnablePlugin>
  val AddPlugin: plugin: PluginInfo -> unit
  val FromText: path: string * content: string -> PluginInfo option
  val LoadPlugins: config: EsbuildConfig -> unit
  val HasPluginsForExtension: plugins: string list -> extension: string -> bool

  val ApplyPlugins:
    plugins: string list -> fileInput: FileTransform -> Async<FileTransform>
