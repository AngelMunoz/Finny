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

  let private tryDownloadEsBuild
    (esbuildVersion: string)
    : Task<string option> =
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


  let private esbuildJsCmd (entryPoint: string, excludes: string seq) =
    printfn "%A" excludes

    let excludes =
      if excludes |> Seq.length > 0 then
        $" --external:{String.Join(',', excludes)} "
      else
        " "

    Cli
      .Wrap(esbuildExec)
      .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
      .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
      .WithArguments(
        $"{entryPoint} --bundle --target=es2015{excludes}--format=esm --outdir=./dist"
      )

  let private esbuildCssCmd (entryPoint: string) =
    Cli
      .Wrap(esbuildExec)
      .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
      .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
      .WithArguments($"{entryPoint} --bundle --minify --outdir=./dist")

  let private pathToFile (staticFilesDir: string) (file: string) =
    Path.Combine(Path.GetFullPath staticFilesDir, file)

  let private getEntryPoints (type': ResourceType) (config: BuildConfig) =
    let context =
      BrowsingContext.New(Configuration.Default)

    let staticFilesDir =
      defaultArg config.StaticFilesDir "./public"

    let indexFile = defaultArg config.IndexFile "index.html"

    let content =
      File.ReadAllText(pathToFile staticFilesDir indexFile)

    let parser = context.GetService<IHtmlParser>()
    let doc = parser.ParseDocument content

    let els =
      match type' with
      | ResourceType.CSS ->
        doc.QuerySelectorAll("[data-entry-point][rel=stylesheet]")
      | ResourceType.JS ->
        doc.QuerySelectorAll("[data-entry-point][type=module]")

    let resourcePredicate (item: Dom.IAttr) : bool =
      match type' with
      | ResourceType.CSS -> item.Name = "href"
      | ResourceType.JS -> item.Name = "src"

    let getPathFromAttribute (el: Dom.IElement) =
      let src =
        match el.Attributes |> Seq.tryFind resourcePredicate with
        | Some attr -> attr.Value
        | None -> ""

      pathToFile staticFilesDir src

    els |> Seq.map getPathFromAttribute

  let insertMapAndCopy config =
    let staticFilesDir =
      defaultArg config.StaticFilesDir "./public"

    let indexFile = defaultArg config.IndexFile "index.html"
    let outDir = defaultArg config.OutDir "./dist"

    let content =
      File.ReadAllText(pathToFile staticFilesDir indexFile)

    let context =
      BrowsingContext.New(Configuration.Default)

    let parser = context.GetService<IHtmlParser>()
    let doc = parser.ParseDocument content
    let script = doc.CreateElement("script")
    script.SetAttribute("type", "importmap")

    task {
      match! Fs.getorCreateLockFile (Fs.Paths.GetFdsConfigPath()) with
      | Ok lock ->
        let map: ImportMap =
          { imports =
              lock
              |> Map.map (fun _ value -> $"{Http.SKYPACK_CDN}{value.pin}")
            scopes = Map.empty }

        script.TextContent <- Json.ToText map
        doc.Head.AppendChild script |> ignore
        let content = doc.ToHtml()

        File.WriteAllText($"{outDir}/{indexFile}", content)
      | Error err ->
        printfn $"Warn: [{err.Message}]"
        ()
    }


  let private buildFiles
    (type': ResourceType)
    (files: string seq)
    (excludes: string seq)
    =
    task {
      if files |> Seq.length > 0 then
        let entrypoints = String.Join(' ', files)

        let cmd =
          match type' with
          | ResourceType.JS ->
            esbuildJsCmd(entrypoints, excludes).ExecuteAsync()
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
      defaultArg buildConfig.EsbuildVersion "0.12.28"

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

      let! excludes =
        task {
          match! Fs.getorCreateLockFile (Fs.Paths.GetFdsConfigPath()) with
          | Ok lock ->

            return lock |> Map.toSeq |> Seq.map (fun (key, _) -> key)
          | Error ex ->
            printfn $"Warn: [{ex.Message}]"
            return Seq.empty
        }

      do!
        Task.WhenAll(
          buildFiles ResourceType.JS jsFiles excludes,
          buildFiles ResourceType.CSS cssFiles Seq.empty
        )
        :> Task

      let opts = EnumerationOptions()
      opts.RecurseSubdirectories <- true

      Directory.EnumerateFiles(Path.GetFullPath(staticFilesDir), "*.*", opts)
      |> Seq.filter
           (fun file ->
             not <| file.Contains(".fable")
             && not <| file.Contains(".js")
             && not <| file.Contains(".css")
             && not <| file.Contains("index.html"))
      |> Seq.iter
           (fun path -> File.Copy(path, $"{outDir}/{Path.GetFileName(path)}"))

      do! insertMapAndCopy buildConfig
    }
