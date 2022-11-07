namespace Perla.Server

open System
open System.Runtime.InteropServices
open Microsoft.AspNetCore.Builder
open Perla.Types
open Perla.Plugins
open Perla.VirtualFs
open Perla.PackageManager.Types
open System.Reactive.Subjects

module Server =
    val GetServerURLs: string -> int -> bool -> string * string

[<Class>]
type Server =
    static member GetServerApp:
        config: PerlaConfig *
        fileChangedEvents: IObservable<FileChangedEvent * FileTransform> *
        compileErrorEvents: IObservable<string option> ->
            WebApplication

    static member GetTestingApp:
        config: PerlaConfig *
        dependencies: (string seq * ImportMap) *
        testEvents: ISubject<TestEvent> *
        fileChangedEvents: IObservable<FileChangedEvent * FileTransform> *
        compileErrorEvents: IObservable<string option> *
        [<Optional>] ?fileGlobs: string seq *
        [<Optional>] ?mochaOptions: Map<string, obj> ->
            WebApplication
