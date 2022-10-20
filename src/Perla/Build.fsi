namespace Perla.Build

open AngleSharp
open AngleSharp.Html.Dom
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open Perla.Types
open Perla.Units
open Perla.PackageManager.Types
open FSharp.UMX


[<Class>]
type Build =
    static member GetIndexFile:
        document: IHtmlDocument *
        cssPaths: string<ServerUrl> seq *
        jsPaths: string<ServerUrl> seq *
        importMap: ImportMap *
        [<Optional>] ?staticDependencies: string seq *
        [<Optional>] ?minify: bool ->
            string
    static member GetEntryPoints: document: IHtmlDocument -> string<ServerUrl> seq * string<ServerUrl> seq

    static member GetExternals: config: PerlaConfig -> string seq

    static member CopyGlobs: config: BuildConfig -> unit
