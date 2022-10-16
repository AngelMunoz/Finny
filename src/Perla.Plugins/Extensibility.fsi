namespace Perla.Plugins.Extensibility

open System
open System.Collections.Concurrent
open System.IO
open System.Runtime.InteropServices
open FSharp.Compiler.Interactive.Shell
open FSharp.Control
open FsToolkit.ErrorHandling
open Perla.Plugins
open Perla.Logger
open System.Collections.Generic

[<Class>]
type Fsi =
    static member GetSession:
        [<Optional>] ?argv: string seq *
        [<Optional>] ?stdout: TextWriter *
        [<Optional>] ?stderr: TextWriter -> FsiEvaluationSession

[<Class>]
type Plugin =
    static member CachedPlugins: unit -> seq<PluginInfo>
    static member FromTextBatch: plugins: seq<string * string> -> unit
    static member AddPlugin: plugin: PluginInfo -> unit
    static member SupportedPlugins: [<Optional>] ?plugins: seq<PluginInfo> -> seq<RunnablePlugin>
    static member ApplyPluginsToFile: fileInput: FileTransform * ?plugins: seq<RunnablePlugin> -> Async<FileTransform>

module Scaffolding =
    [<Literal>]
    val ScaffoldConfiguration: string = "TemplateConfiguration"

    val getConfigurationFromScript: content: string -> obj option
