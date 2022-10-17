namespace Perla.Esbuild

open System.IO
open System.Runtime.InteropServices
open CliWrap
open Perla.Types
open Perla.Units
open Perla.Plugins
open FSharp.UMX

[<RequireQualifiedAccess; Struct>]
type LoaderType =
    | Typescript
    | Tsx
    | Jsx

[<Class>]
type Esbuild =
    static member ProcessJS:
        entryPoint: string * config: EsbuildConfig * outDir: string<SystemPath> * [<Optional>] ?externals: seq<string> ->
            Command
    static member ProcessCss: entryPoint: string * config: EsbuildConfig * outDir: string<SystemPath> -> Command
    static member BuildSingleFile:
        config: EsbuildConfig * content: string * resultsContainer: Stream * ?loader: LoaderType -> Command
    static member GetPlugin: config: EsbuildConfig -> PluginInfo
