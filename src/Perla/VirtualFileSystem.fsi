namespace Perla.VirtualFs

open System
open System.Threading.Tasks

open FSharp.UMX
open FSharp.Control

open Zio
open Zio.FileSystems

open Perla.Types
open Perla.Units
open Perla.Extensibility
open Perla.Plugins

[<Struct>]
type ChangeKind =
    | Created
    | Deleted
    | Renamed
    | Changed

type FileChangedEvent =
    { serverPath: string<ServerUrl>
      userPath: string<UserPath>
      oldPath: string<SystemPath> option
      oldName: string<SystemPath> option
      changeType: ChangeKind
      path: string<SystemPath>
      name: string<SystemPath> }

[<Struct>]
type internal PathInfo =
    { globPath: string<SystemPath>
      localPath: string<UserPath>
      url: string<ServerUrl> }

type internal ApplyPluginsFn = FileTransform -> Async<FileTransform>

[<RequireQualifiedAccess>]
module VirtualFileSystem =

    val internal processFiles:
        plugins: string list ->
        url: string<ServerUrl> ->
        userPath: string<UserPath> ->
        physicalFileSystem: IFileSystem ->
        memoryFileSystem: IFileSystem ->
        applyPlugins: ApplyPluginsFn ->
        globPath: string ->
            Async<unit>

    val internal copyToDisk:
        tempDir: string<SystemPath> * mountedFileSystem: IFileSystem * physicalFileSystem: IFileSystem -> string

    val internal updateInVirtualFs:
        serverFs: IFileSystem -> event: FileChangedEvent * transform: FileTransform -> FileChangedEvent * FileTransform

    val ApplyVirtualOperations:
        plugins: string list -> stream: IObservable<FileChangedEvent> -> IObservable<FileChangedEvent * FileTransform>

    val Mount: config: PerlaConfig -> Async<unit>

    val CopyToDisk: unit -> string

    val GetFileChangeStream:
        mountedDirectories: Map<string<ServerUrl>, string<UserPath>> -> IObservable<FileChangedEvent>

    val TryResolveFile: url: string<ServerUrl> -> byte[] option
