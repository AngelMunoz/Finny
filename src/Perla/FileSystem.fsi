namespace Perla.FileSystem

open System
open System.IO
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open FSharp.UMX
open Perla.Units
open Perla.PackageManager.Types

[<RequireQualifiedAccess>]
module FileSystem =
    module Operators =
        val inline (/): a: string -> b: string -> string

    val AssemblyRoot: string<SystemPath>
    val Database: string<SystemPath>
    val Templates: string<SystemPath>
    val PerlaConfigPath: string<SystemPath>
    val LiveReloadScript: Lazy<string>
    val WorkerScript: Lazy<string>
    val CurrentWorkingDirectory: unit -> string<SystemPath>
    val GetConfigPath: fileName: string -> fromDirectory: string<SystemPath> option -> string<SystemPath>
    val ExtractTemplateZip: name: string<SystemPath> -> stream: Stream -> unit
    val RemoveTemplateDirectory: name: string<SystemPath> -> unit
    val EsbuildBinaryPath: unit -> string<SystemPath>
    val TryReadTsConfig: unit -> string option
    val GetTempDir: unit -> string
    val TplRepositoryChildTemplates: path: string<SystemPath> -> string<SystemPath> seq

[<Class>]
type FileSystem =
    static member PerlaConfigText: ?fromDirectory: string<SystemPath> -> string option
    static member SetCwdToPerlaRoot: ?fromPath: string<SystemPath> -> unit
    static member ImportMap: ?fromDirectory: string<SystemPath> -> ImportMap
    static member SetupEsbuild:
        esbuildVersion: string<Semver> * [<Optional>] ?cancellationToken: CancellationToken -> Task<unit>
    static member WriteImportMap: map: ImportMap * ?fromDirectory: string<SystemPath> -> ImportMap
    static member PathForTemplate: name: string * branch: string * ?tplName: string -> string
    static member WriteTplRepositoryToDisk:
        origin: string<SystemPath> * target: string<UserPath> * ?payload: obj -> unit
    static member GetTemplateScriptContent: name: string * branch: string * tplname: string -> string option
    static member IndexFile: fromConfig: string<SystemPath> -> string
    static member PluginFiles: unit -> (string * string) array
    static member GenerateSimpleFable: path: string<SystemPath> -> Async<unit>
