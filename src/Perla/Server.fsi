namespace Perla.Server

open System
open Microsoft.AspNetCore.Builder
open Perla.Types
open Perla.Plugins
open Perla.VirtualFs

[<Class>]
type Server =
    static member GetServerApp:
        config: PerlaConfig *
        fileChangedEvents: IObservable<FileChangedEvent * FileTransform option> *
        compileErrorEvents: IObservable<string option> ->
            WebApplication
