namespace Perla

open System
open System.IO
open System.Net.Http
open System.Threading.Tasks

open FSharp.Control.Tasks

open AngleSharp
open AngleSharp.Html.Parser

open Types
open Fable
open Esbuild

module Build =

  [<RequireQualifiedAccess>]
  type private ResourceType =
    | JS
    | CSS

    member this.AsString() =
      match this with
      | JS -> "JS"
      | CSS -> "CSS"

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

  let insertMapAndCopy cssFiles config =

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

    let styles =
      [ for file in cssFiles do
          let style = doc.CreateElement("link")
          style.SetAttribute("rel", "stylesheet")
          style.SetAttribute("href", file)
          style ]

    let script = doc.CreateElement("script")
    script.SetAttribute("type", "importmap")

    task {
      match! Fs.getorCreateLockFile (Fs.Paths.GetFdsConfigPath()) with
      | Ok lock ->
        let map: ImportMap =
          { imports = lock.imports
            scopes = lock.scopes }

        script.TextContent <- Json.ToTextMinified map

        for style in styles do
          doc.Head.AppendChild(style) |> ignore

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

  let getExcludes config =
    task {
      match! Fs.getorCreateLockFile (Fs.Paths.GetFdsConfigPath()) with
      | Ok lock ->
        let excludes =
          lock.imports
          |> Map.toSeq
          |> Seq.map (fun (key, _) -> key)

        return
          config.externals
          |> Option.map
               (fun ex ->
                 seq {
                   yield! ex
                   yield! excludes
                 })
          |> Option.defaultValue excludes

      | Error ex ->
        printfn $"Warn: [{ex.Message}]"
        return Seq.empty
    }

  let execBuild (config: FdsConfig) =
    let buildConfig =
      defaultArg config.build (BuildConfig.DefaultConfig())

    let devServer =
      defaultArg config.devServer (DevServerConfig.DefaultConfig())

    let outDir = defaultArg buildConfig.outDir "./dist"

    let esbuildVersion =
      defaultArg buildConfig.esbuildVersion "0.13.1"

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

      let! excludes = getExcludes buildConfig

      let buildConfig =
        { buildConfig with
            externals = excludes |> Some }

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
                     && not <| file.Contains(".ts")
                     && not <| file.Contains(".jsx")
                     && not <| file.Contains(".tsx")
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

      let cssFiles =
        [ for jsFile in jsFiles do
            let name =
              (Path.GetFileName jsFile).Replace(".js", ".css")

            let dirName =
              (Path.GetDirectoryName jsFile)
                .Split(Path.DirectorySeparatorChar)
              |> Seq.last

            $"./{dirName}/{name}".Replace("\\", "/") ]

      do! insertMapAndCopy cssFiles config
    }
