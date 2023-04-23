namespace Perla.Esbuild

open System.Text
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
  | Css


[<Class>]
type Esbuild =
  /// Uses esbuild's build API
  /// This is the most flexible option and allows for simpler customization
  /// This should be performed only when the file system is available
  static member ProcessJS:
    workingDirectory: string *
    entryPoint: string *
    config: EsbuildConfig *
    outDir: string *
    [<Optional>] ?externals: string seq *
    [<Optional>] ?aliases: Map<string<BareImport>, string<ResolutionUrl>> ->
      Command

  static member ProcessCss:
    workingDirectory: string *
    entryPoint: string *
    config: EsbuildConfig *
    outDir: string ->
      Command

  /// Uses esbuild's transform API via stdin/stdout
  /// This means each file will be processed in isolation
  static member BuildSingleFile:
    config: EsbuildConfig *
    content: string *
    resultsContainer: StringBuilder *
    ?loader: LoaderType ->
      Command

  static member GetPlugin: config: EsbuildConfig -> PluginInfo
