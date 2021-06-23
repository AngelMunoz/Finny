namespace FSharp.DevServer



open System
open System.IO
open System.Net.Http
open System.Runtime.InteropServices
open System.Threading.Tasks

open FSharp.Control.Tasks

open AngleSharp
open AngleSharp.Html.Parser

open CliWrap

open ICSharpCode.SharpZipLib.Tar
open ICSharpCode.SharpZipLib.GZip

open Types
open Fable

module Build =
  let private isWindows =
    RuntimeInformation.IsOSPlatform(OSPlatform.Windows)

  [<RequireQualifiedAccess>]
  type private ResourceType =
    | JS
    | CSS

    member this.AsString() =
      match this with
      | JS -> "JS"
      | CSS -> "CSS"

  let private platformString =
    if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
      "windows"
    else if RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
      "linux"
    else if RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
      "darwin"
    else if RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD) then
      "freebsd"
    else
      failwith "Unsupported OS"

  let private archString =
    match RuntimeInformation.OSArchitecture with
    | Architecture.Arm -> "arm"
    | Architecture.Arm64 -> "arm64"
    | Architecture.X64 -> "64"
    | Architecture.X86 -> "32"
    | _ -> failwith "Unsupported Architecture"

  let private tgzDownloadPath =
    Path.Combine("./", ".fsdevserver", "esbuild.tgz")

  let private esbuildExec =
    Path.Combine(
      "./",
      ".fsdevserver",
      "package",
      $"""esbuild{if isWindows then ".exe" else ""}"""
    )

  let private tryDownloadEsBuild (esbuildVersion: string) : Task<string option> =
    let binString = $"esbuild-{platformString}-{archString}"

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

  let private decompressFile (path: Task<string option>) =
    task {
      match! path with
      | Some path ->

        use stream = new GZipInputStream(File.OpenRead path)

        use archive =
          TarArchive.CreateInputTarArchive(stream, Text.Encoding.UTF8)

        archive.ExtractContents(Path.Combine(Path.GetDirectoryName path))
        return Some path
      | None -> return None
    }

  let private cleanup (path: Task<string option>) =
    task {
      match! path with
      | Some path -> File.Delete(path)
      | None -> ()
    }

  let private setupEsbuild (esbuildVersion: string) =
    tryDownloadEsBuild esbuildVersion
    |> decompressFile
    |> cleanup
    |> Async.AwaitTask


  let private esbuildJsCmd (entryPoint: string) =
    Cli
      .Wrap(esbuildExec)
      .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
      .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
      .WithArguments($"{entryPoint} --bundle --minify --target=es2015 --format=esm --outdir=./dist")

  let private esbuildCssCmd (entryPoint: string) =
    Cli
      .Wrap(esbuildExec)
      .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
      .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
      .WithArguments($"{entryPoint} --bundle --minify --outdir=./dist")

  let private getEntryPoints (type': ResourceType) (config: BuildConfig) =
    let context =
      BrowsingContext.New(Configuration.Default)

    let staticFilesDir =
      defaultArg config.StaticFilesDir "./public"

    let indexFile = defaultArg config.IndexFile "index.html"

    let pathToFile (file: string) =
      Path.Combine(Path.GetFullPath staticFilesDir, file)

    let content = File.ReadAllText(pathToFile indexFile)

    let parser = context.GetService<IHtmlParser>()
    let doc = parser.ParseDocument content

    let els =
      match type' with
      | ResourceType.CSS -> doc.QuerySelectorAll("[data-entry-point][rel=stylesheet]")
      | ResourceType.JS -> doc.QuerySelectorAll("[data-entry-point][type=module]")

    let resourcePredicate (item: Dom.IAttr) : bool =
      match type' with
      | ResourceType.CSS -> item.Name = "href"
      | ResourceType.JS -> item.Name = "src"

    let getPathFromAttribute (el: Dom.IElement) =
      let src =
        match el.Attributes |> Seq.tryFind resourcePredicate with
        | Some attr -> attr.Value
        | None -> ""

      pathToFile src

    els |> Seq.map getPathFromAttribute


  let private buildFiles (type': ResourceType) (files: string seq) =
    task {
      if files |> Seq.length > 0 then
        let entrypoints = String.Join(' ', files)

        let cmd =
          match type' with
          | ResourceType.JS -> esbuildJsCmd(entrypoints).ExecuteAsync()
          | ResourceType.CSS -> esbuildCssCmd(entrypoints).ExecuteAsync()

        printfn $"Starting esbuild with pid: [{cmd.ProcessId}]"

        return! cmd.Task :> Task
      else
        printfn $"No Entrypoints for {type'.AsString()} found in index.html"
    }

  let execBuild (buildConfig: BuildConfig) (fableConfig: FableConfig) =
    let staticFilesDir =
      defaultArg buildConfig.StaticFilesDir "./public"

    let outDir = defaultArg buildConfig.OutDir "./dist"

    let esbuildVersion =
      defaultArg buildConfig.EsbuildVersion "0.12.9"

    task {
      let cmdResult =
        (fableCmd (Some false) fableConfig).ExecuteAsync()

      printfn $"Starting Fable with pid: [{cmdResult.ProcessId}]"

      do! cmdResult.Task :> Task

      if not <| File.Exists(esbuildExec) then
        do! setupEsbuild esbuildVersion

      try
        Directory.Delete(outDir, true)
      with
      | ex -> ()

      Directory.CreateDirectory(outDir) |> ignore

      let jsFiles =
        getEntryPoints ResourceType.JS buildConfig

      let cssFiles =
        getEntryPoints ResourceType.CSS buildConfig

      do! Task.WhenAll(buildFiles ResourceType.JS jsFiles, buildFiles ResourceType.CSS cssFiles) :> Task

      let opts = EnumerationOptions()
      opts.RecurseSubdirectories <- true

      Directory.EnumerateFiles(Path.GetFullPath(staticFilesDir), "*.*", opts)
      |> Seq.filter
           (fun file ->
             not <| file.Contains(".fable")
             && not <| file.Contains(".js")
             && not <| file.Contains(".css"))
      |> Seq.iter (fun path -> File.Copy(path, $"{outDir}/{Path.GetFileName(path)}"))
    }
