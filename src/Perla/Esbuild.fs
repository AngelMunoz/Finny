module Perla.Esbuild

open System
open System.IO
open System.Net.Http
open System.Text
open System.Threading.Tasks

open ICSharpCode.SharpZipLib.Tar
open ICSharpCode.SharpZipLib.GZip

open CliWrap

open FsToolkit.ErrorHandling

open Types


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

let addLoader (loader: LoaderType) (args: Builders.ArgumentsBuilder) =
  let loader =
    match loader with
    | Typescript -> "ts"
    | Tsx -> "tsx"
    | Jsx -> "jsx"

  args.Add $"--loader={loader}"

let addInlineSourceMaps (args: Builders.ArgumentsBuilder) =
  args.Add "--sourcemap=inline"


let private tgzDownloadPath =
  Path.Combine(Env.getToolsPath (), "esbuild.tgz")

let esbuildExec =
  let bin = if Env.isWindows then "" else "bin"
  let exec = if Env.isWindows then ".exe" else ""
  Path.Combine(Env.getToolsPath (), "package", bin, $"esbuild{exec}")

let private tryDownloadEsBuild (esbuildVersion: string) : Task<string option> =
  let binString =
    $"esbuild-{Env.platformString}-{Env.archString}"

  let url =
    $"https://registry.npmjs.org/{binString}/-/{binString}-{esbuildVersion}.tgz"

  Directory.CreateDirectory(Path.GetDirectoryName(tgzDownloadPath))
  |> ignore

  task {
    try
      use client = new HttpClient()
      printfn "Downloading esbuild from: %s" url

      use! stream = client.GetStreamAsync(url)
      use file = File.OpenWrite(tgzDownloadPath)

      do! stream.CopyToAsync file
      return Some(file.Name)
    with
    | ex ->
      eprintfn "%O" ex
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
        printfn $"Executing: chmod +x on \"{esbuildExec}\""
        let res = chmodBinCmd().ExecuteAsync()
        do! res.Task :> Task

      return Some path
    | None -> return None
  }

let private cleanup (path: Task<string option>) =
  task {
    match! path with
    | Some path -> File.Delete(path)
    | None -> ()
  }

let setupEsbuild (esbuildVersion: string) =
  if not <| File.Exists(esbuildExec) then
    tryDownloadEsBuild esbuildVersion
    |> decompressFile
    |> cleanup
  else
    Task.FromResult(())



let esbuildJsCmd (entryPoint: string) (config: BuildConfig) =

  let dirName =
    (Path.GetDirectoryName entryPoint)
      .Split(Path.DirectorySeparatorChar)
    |> Seq.last

  let outDir =
    match config.outDir with
    | Some outdir -> Path.Combine(outdir, dirName) |> Some
    | None -> Path.Combine("./dist", dirName) |> Some

  let execBin =
    defaultArg config.esBuildPath esbuildExec

  Cli
    .Wrap(execBin)
    .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
    .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
    .WithArguments(fun args ->
      args.Add(entryPoint)
      |> addEsExternals config.externals
      |> addIsBundle config.bundle
      |> addTarget config.target
      |> addMinify config.minify
      |> addFormat config.format
      |> addOutDir outDir
      |> ignore)

let esbuildCssCmd (entryPoint: string) (config: BuildConfig) =
  let execBin =
    defaultArg config.esBuildPath esbuildExec

  Cli
    .Wrap(execBin)
    .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
    .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
    .WithArguments(fun args ->
      args.Add(entryPoint)
      |> addIsBundle config.bundle
      |> addMinify config.minify
      |> addOutDir config.outDir
      |> ignore)

let private buildSingleFileCmd
  (config: BuildConfig)
  (strio: StringBuilder * StringBuilder)
  (content: string, loader: LoaderType)
  : Command =
  let execBin =
    defaultArg config.esBuildPath esbuildExec

  let (strout, strerr) = strio

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
      |> addInlineSourceMaps
      |> ignore)
    .WithValidation(CommandResultValidation.None)

let tryCompileFile filepath config =
  taskResult {
    let config =
      (defaultArg config (BuildConfig.DefaultConfig()))

    let! res = Fs.tryReadFile filepath
    let strout = StringBuilder()
    let strerr = StringBuilder()

    let execBin =
      defaultArg config.esBuildPath esbuildExec

    if not <| File.Exists(esbuildExec) then
      do! setupEsbuild execBin

    let cmd =
      buildSingleFileCmd config (strout, strerr) res

    do! (cmd.ExecuteAsync()).Task :> Task
    return (strout.ToString(), strerr.ToString())
  }
