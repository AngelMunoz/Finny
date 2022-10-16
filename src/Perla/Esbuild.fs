namespace Perla.Esbuild

open System
open System.IO
open System.Net.Http
open System.Runtime.InteropServices
open System.Text
open System.Threading.Tasks

open ICSharpCode.SharpZipLib.Tar
open ICSharpCode.SharpZipLib.GZip

open CliWrap

open FsToolkit.ErrorHandling

open Perla
open Perla.Types
open Perla.Units
open Perla.Logger
open Perla.FileSystem
open Perla.Plugins
open FSharp.UMX

[<RequireQualifiedAccess; Struct>]
type LoaderType =
  | Typescript
  | Tsx
  | Jsx

[<RequireQualifiedAccess>]
module Esbuild =




  let addEsExternals
    (externals: (string seq))
    (args: Builders.ArgumentsBuilder)
    =

    externals |> Seq.map (fun ex -> $"--external:{ex}") |> args.Add

  let addIsBundle (isBundle: bool) (args: Builders.ArgumentsBuilder) =

    if isBundle then args.Add("--bundle") else args

  let addMinify (minify: bool) (args: Builders.ArgumentsBuilder) =

    if minify then args.Add("--minify") else args

  let addFormat (format: string) (args: Builders.ArgumentsBuilder) =
    args.Add $"--format={format}"

  let addTarget (target: string) (args: Builders.ArgumentsBuilder) =
    args.Add $"--target={target}"

  let addOutDir (outdir: string<SystemPath>) (args: Builders.ArgumentsBuilder) =
    args.Add $"--outdir={outdir}"

  let addOutFile
    (outfile: string<SystemPath>)
    (args: Builders.ArgumentsBuilder)
    =
    args.Add $"--outfile={outfile}"

  /// This is used for known file types when compiling on the fly or at build time
  let addLoader (loader: LoaderType option) (args: Builders.ArgumentsBuilder) =
    match loader with
    | Some loader ->
      let loader =
        match loader with
        | LoaderType.Typescript -> "ts"
        | LoaderType.Tsx -> "tsx"
        | LoaderType.Jsx -> "jsx"

      args.Add $"--loader={loader}"
    | None -> args

  /// This one is used for unknown file assets like png's, svg's font files and similar assets
  let addDefaultFileLoaders
    (loaders: Map<string, string>)
    (args: Builders.ArgumentsBuilder)
    =
    let loaders = loaders |> Map.toSeq

    for (extension, loader) in loaders do
      args.Add $"--loader:{extension}={loader}" |> ignore

    args

  let addJsxFactory (factory: string option) (args: Builders.ArgumentsBuilder) =
    match factory with
    | Some factory -> args.Add $"--jsx-factory={factory}"
    | None -> args

  let addJsxFragment
    (fragment: string option)
    (args: Builders.ArgumentsBuilder)
    =
    match fragment with
    | Some fragment -> args.Add $"--jsx-fragment={fragment}"
    | None -> args

  let addInlineSourceMaps (args: Builders.ArgumentsBuilder) =
    args.Add "--sourcemap=inline"

  let addInjects (injects: string seq) (args: Builders.ArgumentsBuilder) =

    injects |> Seq.map (fun inject -> $"--inject:{inject}") |> args.Add

  let addTsconfigRaw
    (tsconfig: string option)
    (args: Builders.ArgumentsBuilder)
    =
    match tsconfig with
    | Some tsconfig ->
      let tsconfig = tsconfig.Replace("\n", "").Replace("\u0022", "\"")

      args.Add $"""--tsconfig-raw={tsconfig} """
    | None -> args

type Esbuild =

  static member ProcessJS(entryPoint: string,config: EsbuildConfig, outDir: string<SystemPath>, [<Optional>] ?externals: string seq) : Command =

    let execBin = config.esBuildPath |> UMX.untag
    let fileLoaders = config.fileLoaders

    Cli
      .Wrap(execBin)
      .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
      .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
      .WithArguments(fun args ->
        args.Add(entryPoint)
        |> Esbuild.addEsExternals config.externals
        |> Esbuild.addIsBundle true
        |> Esbuild.addTarget config.ecmaVersion
        |> Esbuild.addDefaultFileLoaders fileLoaders
        |> Esbuild.addMinify config.minify
        |> Esbuild.addFormat "esm"
        |> Esbuild.addInjects config.injects
        |> Esbuild.addJsxFactory config.jsxFactory
        |> Esbuild.addJsxFragment config.jsxFragment
        |> Esbuild.addOutDir outDir
        |> ignore)

  static member ProcessCss
    (entryPoint: string, config: EsbuildConfig, outDir: string<SystemPath>)
    =
    let execBin = config.esBuildPath |> UMX.untag
    let fileLoaders = config.fileLoaders

    Cli
      .Wrap(execBin)
      .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
      .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
      .WithArguments(fun args ->
        args.Add(entryPoint)
        |> Esbuild.addIsBundle true
        |> Esbuild.addMinify config.minify
        |> Esbuild.addOutDir outDir
        |> Esbuild.addDefaultFileLoaders fileLoaders
        |> ignore)

  static member  BuildSingleFile(config: EsbuildConfig, content: string, resultsContainer: Stream, [<Optional>] ?loader: LoaderType)
    : Command =
    let execBin = config.esBuildPath |> UMX.untag
    let tsconfig = FileSystem.TryReadTsConfig()

    Cli
      .Wrap(execBin)
      .WithStandardInputPipe(PipeSource.FromString(content))
      .WithStandardOutputPipe(PipeTarget.ToStream(resultsContainer))
      .WithStandardErrorPipe(
        PipeTarget.ToDelegate(fun msg -> Logger.log($"[bold red]{msg}[/]", target = PrefixKind.Esbuild))
      )
      .WithArguments(fun args ->
        args
        |> Esbuild.addTarget config.ecmaVersion
        |> Esbuild.addLoader loader
        |> Esbuild.addFormat "esm"
        |> Esbuild.addJsxFactory config.jsxFactory
        |> Esbuild.addJsxFragment config.jsxFragment
        |> Esbuild.addInlineSourceMaps
        |> Esbuild.addTsconfigRaw tsconfig
        |> ignore)
      .WithValidation(CommandResultValidation.None)

  static member GetPlugin(config: EsbuildConfig) : PluginInfo =
    let shouldTransform: FilePredicate =
      fun args ->
        [ ".jsx"; ".tsx"; ".ts"; ".js"; ".css" ] |> List.contains args.extension

    let transform: TransformTask =
      fun args ->
        task {
          let loader =
            match args.extension with
            | ".css"
            | ".js" -> None
            | ".jsx" -> Some LoaderType.Jsx
            | ".tsx" -> Some LoaderType.Tsx
            | ".ts" -> Some LoaderType.Typescript
            | _ -> None

          use resultsContainer = new MemoryStream()
          let result = Esbuild.BuildSingleFile(config, args.content, resultsContainer, ?loader = loader)
          let! _ = result.ExecuteAsync()
          use transformContent = new StreamReader(resultsContainer)
          let! result = transformContent.ReadToEndAsync()
          return { content = result; extension = ".js" }
        }

    plugin "perla-esbuild-plugin" {
      should_process_file shouldTransform
      with_transform transform
    }
