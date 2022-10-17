namespace Perla.VirtualFs

open System
open Fake.IO
open Perla.Plugins
open Zio
open Zio.FileSystems
open Perla
open Perla.Types
open Perla.Units
open Perla.FileSystem
open Perla.Extensibility
open FSharp.UMX
open FSharp.Control
open FSharp.Control.Reactive
open Fake.IO.Globbing.Operators

[<Struct>]
type ChangeKind =
    | Created
    | Deleted
    | Renamed
    | Changed

type FileChangedEvent =
    { serverPath: string<ServerUrl>
      userPath: string<UserPath>
      oldPath: string option
      oldName: string option
      changeType: ChangeKind
      path: string
      name: string }

type IFileWatcher =
    inherit IDisposable
    abstract member FileChanged: IObservable<FileChangedEvent>

[<RequireQualifiedAccess>]
module VirtualFileSystem =

    val GetFileChangeStream:
        mountedDirectories: Map<string<ServerUrl>, string<UserPath>> -> IObservable<FileChangedEvent>

    val TryResolveFile: url: string<ServerUrl> -> string option

    val ApplyVirtualOperations:
        stream: IObservable<FileChangedEvent> -> IObservable<FileChangedEvent * FileTransform option>

    val Mount: config: PerlaConfig -> Async<unit>

    val CopyToDisk: unit -> string
