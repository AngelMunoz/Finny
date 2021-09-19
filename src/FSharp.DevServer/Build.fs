namespace FSharp.DevServer

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
    let external =
      match config.externals with
      | Some externals when externals |> Seq.length > 0 ->
        $" --external:{String.Join(',', externals)} "
      | _ -> ""

    let bundle =
      config.bundle
      |> Option.map (fun bundle -> if bundle then $"--bundle" else "")
      |> Option.defaultValue "--bundle"

    let target =
      config.target
      |> Option.map (fun target -> $"--target={target}")
      |> Option.defaultValue "--target=es2015"

    let minify =
      config.minify
      |> Option.map (fun minify -> if minify then $"--minify" else "")
      |> Option.defaultValue "--minify"

    let format =
      config.format
      |> Option.map (fun format -> $"--format={format}")
      |> Option.defaultValue "--format=esm"

    let outDir =
      config.outDir
      |> Option.map (fun outDir -> $"--outdir={outDir}")
      |> Option.defaultValue "--outdir=./dist"

    let execBin =
      config.esBuildPath
      |> Option.defaultValue esbuildExec

    let args =
      $"{entryPoint} {bundle} {target} {external} {format} {outDir} {minify}"

    printfn "%s" args

    Cli
      .Wrap(execBin)
      .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
      .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
      .WithArguments(args)

  let private esbuildCssCmd (entryPoint: string) (config: BuildConfig) =

    let bundle =
      config.bundle
      |> Option.map (fun bundle -> if bundle then $"--bundle" else "")
      |> Option.defaultValue "--bundle"

    let minify =
      config.minify
      |> Option.map (fun minify -> if minify then $"--minify" else "")
      |> Option.defaultValue "--minify"

    let outDir =
      config.outDir
      |> Option.map (fun outDir -> $"--outDir={outDir}")
      |> Option.defaultValue "--outDir=./dist"

    let execBin =
      config.esBuildPath
      |> Option.defaultValue esbuildExec

    let args =
      $"{entryPoint} {bundle} {minify} {outDir}"

    printfn "%s" args

    Cli
      .Wrap(execBin)
      .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
      .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
      .WithArguments(args)

  let private pathToFile (staticFilesDir: string) (file: string) =
    Path.Combine(Path.GetFullPath staticFilesDir, file)

  let private getEntryPoints (type': ResourceType) (config: BuildConfig) =
    let context =
      BrowsingContext.New(Configuration.Default)

    let staticFilesDir =
      defaultArg config.staticFilesDir "./public"

    let indexFile = defaultArg config.indexFile "index.html"

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
      defaultArg config.staticFilesDir "./public"

    let indexFile = defaultArg config.indexFile "index.html"
    let outDir = defaultArg config.outDir "./dist"

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

  let execBuild (buildConfig: BuildConfig) (fableConfig: FableConfig) =
    let staticFilesDir =
      defaultArg buildConfig.staticFilesDir "./public"

    let outDir = defaultArg buildConfig.outDir "./dist"

    let esbuildVersion =
      defaultArg buildConfig.esbuildVersion "0.12.28"

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

      let config =
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
          buildFiles ResourceType.JS jsFiles config,
          buildFiles ResourceType.CSS cssFiles config
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
