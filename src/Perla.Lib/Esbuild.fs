namespace Perla.Lib

open System
open System.IO
open System.Net.Http
open System.Text
open System.Threading.Tasks

open ICSharpCode.SharpZipLib.Tar
open ICSharpCode.SharpZipLib.GZip

open CliWrap

open FsToolkit.ErrorHandling

open Perla.Lib
open Types
open Logger

module Esbuild =
  let addEsExternals
    (externals: (string seq) option)
    (args: Builders.ArgumentsBuilder)
    =
    let externals = defaultArg externals Seq.empty

    externals
    |> Seq.map (fun ex -> $"--external:{ex}")
    |> args.Add

  let addIsBundle (isBundle: bool option) (args: Builders.ArgumentsBuilder) =
    let isBundle = defaultArg isBundle true

    if isBundle then
      args.Add("--bundle")
    else
      args

  let addMinify (minify: bool option) (args: Builders.ArgumentsBuilder) =
    let minify = defaultArg minify true

    if minify then
      args.Add("--minify")
    else
      args

  let addFormat (format: string option) (args: Builders.ArgumentsBuilder) =
    let format = defaultArg format "esm"
    args.Add $"--format={format}"

  let addTarget (target: string option) (args: Builders.ArgumentsBuilder) =
    let target = defaultArg target "es2015"

    args.Add $"--target={target}"

  let addOutDir (outdir: string option) (args: Builders.ArgumentsBuilder) =
    let outdir = defaultArg outdir "./dist"

    args.Add $"--outdir={outdir}"

  let addOutFile (outfile: string) (args: Builders.ArgumentsBuilder) =
    args.Add $"--outfile={outfile}"

  /// This is used for known file types when compiling on the fly or at build time
  let addLoader (loader: LoaderType) (args: Builders.ArgumentsBuilder) =
    let loader =
      match loader with
      | LoaderType.Typescript -> "ts"
      | LoaderType.Tsx -> "tsx"
      | LoaderType.Jsx -> "jsx"

    args.Add $"--loader={loader}"

  /// This one is used for unknown file assets like png's, svg's font files and similar assets
  let addDefaultFileLoaders
    (loaders: Map<string, string>)
    (args: Builders.ArgumentsBuilder)
    =
    let loaders = loaders |> Map.toSeq

    for (extension, loader) in loaders do
      args.Add $"--loader:{extension}={loader}"
      |> ignore

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

  let addInjects
    (injects: string seq option)
    (args: Builders.ArgumentsBuilder)
    =
    let injects = defaultArg injects Seq.empty

    injects
    |> Seq.map (fun inject -> $"--inject:{inject}")
    |> args.Add

  let addTsconfigRaw
    (tsconfig: string option)
    (args: Builders.ArgumentsBuilder)
    =
    match tsconfig with
    | Some tsconfig ->
      let tsconfig = tsconfig.Replace("\n", "").Replace("\u0022", "\"")

      args.Add $"""--tsconfig-raw={tsconfig} """
    | None -> args

  let private tgzDownloadPath =
    Path.Combine(Path.PerlaRootDirectory, "esbuild.tgz")

  let esbuildExec =
    let bin = if Env.isWindows then "" else "bin"
    let exec = if Env.isWindows then ".exe" else ""
    Path.Combine(Path.PerlaRootDirectory, "package", bin, $"esbuild{exec}")

  let private tryDownloadEsBuild
    (esbuildVersion: string)
    : Task<string option> =
    let binString = $"esbuild-{Env.platformString}-{Env.archString}"

    let url =
      $"https://registry.npmjs.org/{binString}/-/{binString}-{esbuildVersion}.tgz"

    Directory.CreateDirectory(Path.GetDirectoryName(tgzDownloadPath))
    |> ignore

    task {
      try
        use client = new HttpClient()
        Logger.log $"Downloading esbuild from: {url}"

        use! stream = client.GetStreamAsync(url)
        use file = File.OpenWrite(tgzDownloadPath)

        do! stream.CopyToAsync file
        Logger.log $"Downloaded esbuild to: {file.Name}"
        return Some(file.Name)
      with
      | ex ->
        Logger.log ($"Failed to download esbuild from: {url}", ex)
        return None
    }

  let private chmodBinCmd () =
    Cli
      .Wrap("chmod")
      .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
      .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
      .WithArguments($"+x {esbuildExec}")

  let private decompressFile (path: Task<string option>) =
    task {
      match! path with
      | Some path ->

        use stream = new GZipInputStream(File.OpenRead path)

        use archive =
          TarArchive.CreateInputTarArchive(stream, Text.Encoding.UTF8)

        archive.ExtractContents(Path.Combine(Path.GetDirectoryName path))

        if Env.isWindows |> not then
          Logger.log $"Executing: chmod +x on \"{esbuildExec}\""
          let res = chmodBinCmd().ExecuteAsync()
          do! res.Task :> Task

        return Some path
      | None -> return None
    }

  let private cleanup (path: Task<string option>) =
    task {
      Logger.log "Cleaning up!"

      match! path with
      | Some path -> File.Delete(path)
      | None -> ()

      Logger.log "This setup should happen once per machine"
      Logger.log "If you see it often please report a bug."
    }

  let setupEsbuild (esbuildVersion: string) =
    Logger.log "Checking whether esbuild is present..."

    if not <| File.Exists(esbuildExec) then
      Logger.log "esbuild is not present, setting esbuild..."

      Logger.spinner (
        "esbuild is not present, setting esbuild...",
        fun context ->
          context.Status <- "Downloading esbuild..."

          tryDownloadEsBuild esbuildVersion
          |> (fun path ->
            context.Status <- "Extracting esbuild..."
            path)
          |> decompressFile
          |> (fun path ->
            context.Status <- "Cleaning up extra files..."
            path)
          |> cleanup
      )
    else
      Logger.log "esbuild is present."
      Task.FromResult(())

  let private getDefaultLoders config =
    config.fileLoaders
    |> Option.defaultValue (BuildConfig.DefaultFileLoaders())

  let esbuildJsCmd (entryPoint: string) (config: BuildConfig) =

    let dirName =
      (Path.GetDirectoryName entryPoint)
        .Split(Path.DirectorySeparatorChar)
      |> Seq.last

    let outDir =
      match config.outDir with
      | Some outdir -> Path.Combine(outdir, dirName) |> Some
      | None -> Path.Combine("./dist", dirName) |> Some

    let execBin = defaultArg config.esBuildPath esbuildExec

    let fileLoaders = getDefaultLoders config

    Cli
      .Wrap(execBin)
      .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
      .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
      .WithArguments(fun args ->
        args.Add(entryPoint)
        |> addEsExternals config.externals
        |> addIsBundle config.bundle
        |> addTarget config.target
        |> addDefaultFileLoaders fileLoaders
        |> addMinify config.minify
        |> addFormat config.format
        |> addInjects config.injects
        |> addJsxFactory config.jsxFactory
        |> addJsxFragment config.jsxFragment
        |> addOutDir outDir
        |> ignore)

  let esbuildCssCmd (entryPoint: string) (config: BuildConfig) =
    let execBin = defaultArg config.esBuildPath esbuildExec

    let fileLoaders = getDefaultLoders config

    Cli
      .Wrap(execBin)
      .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
      .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
      .WithArguments(fun args ->
        args.Add(entryPoint)
        |> addIsBundle config.bundle
        |> addMinify config.minify
        |> addOutDir config.outDir
        |> addDefaultFileLoaders fileLoaders
        |> ignore)

  let private buildSingleFileCmd
    (config: BuildConfig)
    (strio: StringBuilder * StringBuilder)
    (content: string, loader: LoaderType)
    : Command =
    let execBin = defaultArg config.esBuildPath esbuildExec

    let tsconfig = Fs.tryGetTsconfigFile ()
    let (strout, strerr) = strio

    let fileLoaders = getDefaultLoders config

    Cli
      .Wrap(execBin)
      .WithStandardInputPipe(PipeSource.FromString(content))
      .WithStandardOutputPipe(PipeTarget.ToStringBuilder(strout))
      .WithStandardErrorPipe(PipeTarget.ToStringBuilder(strerr))
      .WithArguments(fun args ->
        args
        |> addTarget config.target
        |> addLoader loader
        |> addFormat ("esm" |> Some)
        |> addJsxFactory config.jsxFactory
        |> addJsxFragment config.jsxFragment
        |> addInlineSourceMaps
        |> addTsconfigRaw tsconfig
        |> ignore)
      .WithValidation(CommandResultValidation.None)

  let tryCompileFile filepath config =
    taskResult {
      let config = (defaultArg config (BuildConfig.DefaultConfig()))

      let! res = Fs.tryReadFile filepath
      let strout = StringBuilder()
      let strerr = StringBuilder()
      let (_, loader) = res

      let cmd = buildSingleFileCmd config (strout, strerr) res

      do! (cmd.ExecuteAsync()).Task :> Task

      let strout = strout.ToString()
      let strerr = strerr.ToString()
      let injects = defaultArg config.injects (Seq.empty)

      let strout =
        match loader with
        | LoaderType.Jsx
        | LoaderType.Tsx ->
          try
            let injects = injects |> Seq.map File.ReadAllText

            let injects = String.Join('\n', injects)
            $"{injects}\n{strout}"
          with
          | ex ->
            let injects =
              injects
              |> Seq.fold (fun current next -> $"{current};{next}") ""

            Logger.serve ($"failed to add injects from file {injects}", ex)
            strout
        | _ -> strout

      return (strout, strerr)
    }
