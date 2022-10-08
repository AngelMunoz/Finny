namespace Perla.Plugins.Extensibility

open System
open System.Collections.Concurrent
open System.IO
open FSharp.Compiler.Interactive.Shell
open FSharp.Control
open FsToolkit.ErrorHandling
open Perla.Plugins
open Perla.Logger

[<Class>]
type Fsi =
    static member getSession:
        ?argv: #seq<string> * ?stdout: TextWriter * ?stderr: TextWriter -> FsiEvaluationSession when 'a :> seq<string>

[<Class>]
type Plugin =
    static member PluginCache: Lazy<ConcurrentDictionary<PluginInfo, FsiEvaluationSession>>
    static member getCachedPlugins: unit -> seq<PluginInfo>
    static member fromCache: pluginName: string * path: string -> PluginInfo option
    static member fromText: name: string * content: string * ?skipCache: bool -> PluginInfo option
    static member loadTextBatch: plugins: seq<{| content: string; name: string |}> -> seq<PluginInfo>
    static member getSupportedPlugins: ?plugins: seq<PluginInfo> -> seq<RunnablePlugin>
    static member applyPluginsToFile: fileInput: FileTransform * ?plugins: seq<RunnablePlugin> -> Async<FileTransform>

module Scaffolding =
    [<Literal>]
    val ScaffoldConfiguration: string = "TemplateConfiguration"

    val getConfigurationFromScript: content: string -> obj option
