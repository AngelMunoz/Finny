namespace Perla

open System
open System.IO
open System.Net.Http
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

  [<RequireQualifiedAccess>]
  type private ResourceType =
    | JS
    | CSS

    member this.AsString() =
      match this with
      | JS -> "JS"
      | CSS -> "CSS"

  let private addEsExternals
    (externals: (string seq) option)
    (args: Builders.ArgumentsBuilder)
    =
    let externals = defaultArg externals Seq.empty

    externals
    |> Seq.map (fun ex -> $"--external:{ex}")
    |> args.Add

  let private addIsBundle
    (isBundle: bool option)
    (args: Builders.ArgumentsBuilder)
    =
    let isBundle = defaultArg isBundle true

    if isBundle then
      args.Add("--bundle")
    else
      args

  let private addMinify
    (minify: bool option)
    (args: Builders.ArgumentsBuilder)
    =
    let minify = defaultArg minify true

    if minify then
      args.Add("--minify")
    else
      args

  let private addFormat
    (format: string option)
    (args: Builders.ArgumentsBuilder)
    =
    let format = defaultArg format "esm"
    args.Add $"--format={format}"

  let private addTarget
    (target: string option)
    (args: Builders.ArgumentsBuilder)
    =
    let target = defaultArg target "es2015"

    args.Add $"--target={target}"

  let private addOutDir
    (outdir: string option)
    (args: Builders.ArgumentsBuilder)
    =
    let outdir = defaultArg outdir "./dist"

    args.Add $"--outdir={outdir}"


  let private tgzDownloadPath =
    Path.Combine(Env.getToolsPath (), "esbuild.tgz")

  let private esbuildExec =
    let bin = if Env.isWindows then "" else "bin"
    let exec = if Env.isWindows then ".exe" else ""
    Path.Combine(Env.getToolsPath (), "package", bin, $"esbuild{exec}")

  let private tryDownloadEsBuild
    (esbuildVersion: string)
    : Task<string option> =
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

  let private setupEsbuild (esbuildVersion: string) =
    tryDownloadEsBuild esbuildVersion
    |> decompressFile
    |> cleanup
    |> Async.AwaitTask


  let private esbuildJsCmd (entryPoint: string) (config: BuildConfig) =

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
        |> addOutDir config.outDir
        |> ignore)

  let private esbuildCssCmd (entryPoint: string) (config: BuildConfig) =
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

  let private getEntryPoints (type': ResourceType) (config: FdsConfig) =
    let context =
      BrowsingContext.New(Configuration.Default)

    let indexFile = defaultArg config.index "index.html"

    let content =
      File.ReadAllText(Path.GetFullPath(indexFile))

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

      Path.GetFullPath(src)

    els |> Seq.map getPathFromAttribute

  let insertMapAndCopy config =

    let indexFile = defaultArg config.index "index.html"

    let outDir =
      match config.build with
      | Some config -> config.outDir |> Option.defaultValue "./dist"
      | None -> "./dist"

    let content =
      File.ReadAllText(Path.GetFullPath(indexFile))

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
          { imports = lock |> Map.map (fun _ value -> value.pin)
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
    (config: BuildConfig)
    =
    task {
      if files |> Seq.length > 0 then
        let entrypoints = String.Join(' ', files)

        let cmd =
          match type' with
          | ResourceType.JS ->
            let tsk = config |> esbuildJsCmd entrypoints
            tsk.ExecuteAsync()
          | ResourceType.CSS ->
            let tsk = config |> esbuildCssCmd entrypoints
            tsk.ExecuteAsync()

        printfn $"Starting esbuild with pid: [{cmd.ProcessId}]"

        return! cmd.Task :> Task
      else
        printfn $"No Entrypoints for {type'.AsString()} found in index.html"
    }

  let execBuild (config: FdsConfig) =
    let buildConfig =
      defaultArg config.build (BuildConfig.DefaultConfig())

    let devServer =
      defaultArg config.devServer (DevServerConfig.DefaultConfig())

    let outDir = defaultArg buildConfig.outDir "./dist"

    let esbuildVersion =
      defaultArg buildConfig.esbuildVersion "0.12.28"

    task {
      match config.fable with
      | Some fable ->
        let cmdResult =
          (fableCmd (Some false) fable).ExecuteAsync()

        printfn $"Starting Fable with pid: [{cmdResult.ProcessId}]"

        do! cmdResult.Task :> Task
      | None -> printfn "No Fable configuration provided, skipping fable"

      if not <| File.Exists(esbuildExec) then
        do! setupEsbuild esbuildVersion

      try
        Directory.Delete(outDir, true)
      with
      | ex -> ()

      Directory.CreateDirectory(outDir) |> ignore

      let jsFiles = getEntryPoints ResourceType.JS config

      let cssFiles = getEntryPoints ResourceType.CSS config

      let! excludes =
        task {
          match! Fs.getorCreateLockFile (Fs.Paths.GetFdsConfigPath()) with
          | Ok lock ->

            return lock |> Map.toSeq |> Seq.map (fun (key, _) -> key)
          | Error ex ->
            printfn $"Warn: [{ex.Message}]"
            return Seq.empty
        }

      let buildConfig =
        { buildConfig with
            externals =
              buildConfig.externals
              |> Option.map
                   (fun ex ->
                     seq {
                       yield! ex
                       yield! excludes
                     })
              |> Option.orElse (Some excludes) }

      do!
        Task.WhenAll(
          buildFiles ResourceType.JS jsFiles buildConfig,
          buildFiles ResourceType.CSS cssFiles buildConfig
        )
        :> Task

      let opts = EnumerationOptions()
      opts.RecurseSubdirectories <- true

      let getDirectories (map: Map<string, string>) =
        seq {
          for key in map.Keys do
            yield!
              Directory.EnumerateFiles(Path.GetFullPath(key), "*.*", opts)
              |> Seq.filter
                   (fun file ->
                     not <| file.Contains(".fable")
                     && not <| file.Contains("bin")
                     && not <| file.Contains("obj")
                     && not <| file.Contains(".fsproj")
                     && not <| file.Contains(".fs")
                     && not <| file.Contains(".js")
                     && not <| file.Contains(".css")
                     && not <| file.Contains("index.html"))
        }

      let copyMountedFiles (dirs: string seq) =
        dirs
        |> Seq.iter
             (fun path -> File.Copy(path, $"{outDir}/{Path.GetFileName(path)}"))

      devServer.mountDirectories
      |> Option.map getDirectories
      |> Option.map copyMountedFiles
      |> ignore

      do! insertMapAndCopy config
    }
