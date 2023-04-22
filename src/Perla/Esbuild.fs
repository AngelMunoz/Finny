namespace Perla.Esbuild

open System
open System.Runtime.InteropServices
open CliWrap

open FsToolkit.ErrorHandling
open FSharp.UMX

open Perla
open Perla.Types
open Perla.Units
open Perla.Logger
open Perla.FileSystem
open Perla.Plugins
open System.Text

[<RequireQualifiedAccess; Struct>]
type LoaderType =
  | Typescript
  | Tsx
  | Jsx
  | Css

[<RequireQualifiedAccess>]
module Esbuild =

  let addEsExternals (externals: string seq) (args: Builders.ArgumentsBuilder) =
    externals |> Seq.map (fun ex -> $"--external:{ex}") |> args.Add

  let addIsBundle (isBundle: bool) (args: Builders.ArgumentsBuilder) =

    if isBundle then args.Add("--bundle") else args

  let addMinify (minify: bool) (args: Builders.ArgumentsBuilder) =

    if minify then args.Add("--minify") else args

  let addFormat (format: string) (args: Builders.ArgumentsBuilder) =
    args.Add $"--format={format}"

  let addTarget (target: string) (args: Builders.ArgumentsBuilder) =
    args.Add $"--target={target}"

  let addOutDir (outDir: string) (args: Builders.ArgumentsBuilder) =
    args.Add $"--outdir={outDir}"

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
        | LoaderType.Css -> "css"

      args.Add $"--loader={loader}"
    | None -> args

  /// This one is used for unknown file assets like pngs, svgs font files and similar assets
  let addDefaultFileLoaders
    (loaders: Map<string, string>)
    (args: Builders.ArgumentsBuilder)
    =
    let loaders = loaders |> Map.toSeq

    for extension, loader in loaders do
      args.Add $"--loader:{extension}={loader}" |> ignore

    args

  let addJsxAutomatic (addAutomatic: bool) (args: Builders.ArgumentsBuilder) =
    if addAutomatic then args.Add "--jsx=automatic" else args

  let addJsxImportSource
    (importSource: string option)
    (args: Builders.ArgumentsBuilder)
    =
    match importSource with
    | Some source -> args.Add $"--jsx-import-source={source}"
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

  let addAliases
    (aliases: Map<string<BareImport>, string<ResolutionUrl>> option)
    (args: Builders.ArgumentsBuilder)
    =
    match aliases with
    | Some aliases ->
      for KeyValue(alias, path) in aliases do
        if (UMX.untag path).StartsWith("./") then
          args.Add $"--alias:{alias}={path}" |> ignore
    | None -> ()

    args

  let addKeepNames (args: Builders.ArgumentsBuilder) = args.Add "--keep-names"

type Esbuild =

  static member ProcessJS
    (
      workingDirectory: string,
      entryPoint: string,
      config: EsbuildConfig,
      outDir: string,
      [<Optional>] ?externals: string seq,
      [<Optional>] ?aliases: Map<string<BareImport>, string<ResolutionUrl>>
    ) : Command =

    let execBin = config.esBuildPath |> UMX.untag
    let fileLoaders = config.fileLoaders

    Cli
      .Wrap(execBin)
      // ensure esbuild is called where the actual sources are
      .WithWorkingDirectory(UMX.untag workingDirectory)
      .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
      .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
      .WithArguments(fun args ->
        args.Add(entryPoint)
        |> Esbuild.addEsExternals (defaultArg externals config.externals)
        |> Esbuild.addIsBundle true
        |> Esbuild.addTarget config.ecmaVersion
        |> Esbuild.addDefaultFileLoaders fileLoaders
        |> Esbuild.addMinify config.minify
        |> Esbuild.addFormat "esm"
        |> Esbuild.addJsxAutomatic config.jsxAutomatic
        |> Esbuild.addJsxImportSource config.jsxImportSource
        |> Esbuild.addOutDir outDir
        |> Esbuild.addAliases aliases
        |> ignore)

  static member ProcessCss
    (
      workingDirectory: string,
      entryPoint: string,
      config: EsbuildConfig,
      outDir: string
    ) =
    let execBin = config.esBuildPath |> UMX.untag
    let fileLoaders = config.fileLoaders

    Cli
      .Wrap(execBin)
      // ensure esbuild is called where the actual sources are
      .WithWorkingDirectory(workingDirectory)
      .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
      .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
      .WithArguments(fun args ->
        args.Add(entryPoint)
        |> Esbuild.addIsBundle true
        |> Esbuild.addMinify config.minify
        |> Esbuild.addOutDir outDir
        |> Esbuild.addDefaultFileLoaders fileLoaders
        |> ignore)

  static member BuildSingleFile
    (
      config: EsbuildConfig,
      content: string,
      resultsContainer: StringBuilder,
      [<Optional>] ?loader: LoaderType
    ) : Command =
    let execBin = config.esBuildPath |> UMX.untag
    let tsconfig = FileSystem.TryReadTsConfig()

    Cli
      .Wrap(execBin)
      .WithStandardInputPipe(PipeSource.FromString(content))
      .WithStandardOutputPipe(PipeTarget.ToStringBuilder(resultsContainer))
      .WithStandardErrorPipe(
        PipeTarget.ToDelegate(fun msg ->
          Logger.logCustom (
            $"[bold red]{msg}[/]",
            escape = true,
            prefixes = [ PrefixKind.Log; PrefixKind.Esbuild ]
          ))
      )
      .WithArguments(fun args ->
        args
        |> Esbuild.addTarget config.ecmaVersion
        |> Esbuild.addLoader loader
        |> Esbuild.addFormat "esm"
        |> Esbuild.addMinify config.minify
        |> Esbuild.addJsxAutomatic config.jsxAutomatic
        |> Esbuild.addJsxImportSource config.jsxImportSource
        |> Esbuild.addTsconfigRaw tsconfig
        |> Esbuild.addKeepNames
        |> ignore)
      .WithValidation(CommandResultValidation.None)

  static member GetPlugin(config: EsbuildConfig) : PluginInfo =
    let shouldTransform: FilePredicate =
      fun extension ->
        [ ".jsx"; ".tsx"; ".ts"; ".css"; ".js" ] |> List.contains extension

    let transform: TransformTask =
      fun args -> task {
        let loader =
          match args.extension with
          | ".css" -> Some LoaderType.Css
          | ".jsx" -> Some LoaderType.Jsx
          | ".tsx" -> Some LoaderType.Tsx
          | ".ts" -> Some LoaderType.Typescript
          | ".js" -> None
          | _ -> None

        let resultsContainer = StringBuilder()

        let result =
          Esbuild.BuildSingleFile(
            config,
            args.content,
            resultsContainer,
            ?loader = loader
          )

        let! _ = result.ExecuteAsync()

        return {
          content = resultsContainer.ToString()
          extension = if args.extension = ".css" then ".css" else ".js"
        }
      }

    plugin Constants.PerlaEsbuildPluginName {
      should_process_file shouldTransform
      with_transform transform
    }
