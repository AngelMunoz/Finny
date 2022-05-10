namespace Perla.Lib

open System
open System.IO
open System.Net.Http
open System.Threading.Tasks

open AngleSharp
open AngleSharp.Html.Parser

open Types
open Fable
open Esbuild
open Logger

module Build =

  [<RequireQualifiedAccess>]
  type private ResourceType =
    | JS
    | CSS

    member this.AsString() =
      match this with
      | JS -> "JS"
      | CSS -> "CSS"

  let private getEntryPoints (type': ResourceType) (config: PerlaConfig) =
    let context = BrowsingContext.New(Configuration.Default)

    let indexFile = defaultArg config.index "index.html"

    let content = File.ReadAllText(Path.GetFullPath(indexFile))

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
      config.build
      |> Option.map (fun build -> build.outDir)
      |> Option.flatten
      |> Option.defaultValue "./dist"

    let content = File.ReadAllText(Path.GetFullPath(indexFile))

    let context = BrowsingContext.New(Configuration.Default)

    let parser = context.GetService<IHtmlParser>()
    let doc = parser.ParseDocument content

    let styles =
      [ for (file: string) in cssFiles do
          let file =
            if file.EndsWith("x") then
              file.Substring(0, file.Length - 1)
            else
              file

          let style = doc.CreateElement("link")
          style.SetAttribute("rel", "stylesheet")
          style.SetAttribute("href", file)
          style ]

    let script = doc.CreateElement("script")
    script.SetAttribute("type", "importmap")

    doc.Body.QuerySelectorAll("[data-entry-point][type=module]")
    |> Seq.iter (fun el ->
      match el.GetAttribute("src") |> Option.ofObj with
      | Some src ->
        el.SetAttribute(
          "src",
          if src.EndsWith("x") then
            src.Substring(0, src.Length - 1)
          else
            src
        )
      | None -> ())

    task {
      match! Fs.getOrCreateLockFile (System.IO.Path.GetPerlaConfigPath()) with
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
      | Error err -> Logger.build ("Failed to get or create lock file", err)
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

        Logger.build $"Starting esbuild with pid: [{cmd.ProcessId}]"

        return! cmd.Task :> Task
      else
        Logger.build
          $"No Entrypoints for {type'.AsString()} found in index.html"
    }

  let getExcludes config =
    task {
      match! Fs.getOrCreateLockFile (System.IO.Path.GetPerlaConfigPath()) with
      | Ok lock ->
        let excludes =
          lock.imports
          |> Map.toSeq
          |> Seq.map (fun (key, _) -> key)

        return
          config.externals
          |> Option.map (fun ex ->
            seq {
              yield! ex
              yield! excludes
            })
          |> Option.defaultValue excludes

      | Error ex ->
        Logger.build ("Failed to get or create lock file", ex)
        return Seq.empty
    }

  let execBuild (config: PerlaConfig) =
    let buildConfig = defaultArg config.build (BuildConfig.DefaultConfig())

    let devServer =
      defaultArg config.devServer (DevServerConfig.DefaultConfig())

    let outDir = defaultArg buildConfig.outDir "./dist"

    let esbuildVersion =
      defaultArg buildConfig.esbuildVersion Constants.Esbuild_Version

    let copyExcludes =
      match buildConfig.copyPaths with
      | None -> BuildConfig.DefaultExcludes()
      | Some paths ->
        paths.excludes
        |> Option.map List.ofSeq
        |> Option.defaultValue (BuildConfig.DefaultExcludes())

    let copyIncludes =
      match buildConfig.copyPaths with
      | None -> Seq.empty
      | Some paths -> paths.includes |> Option.defaultValue Seq.empty

    task {
      match config.fable with
      | Some fable ->
        let cmdResult = (fableCmd (Some false) fable).ExecuteAsync()

        Logger.build $"Starting Fable with pid: [{cmdResult.ProcessId}]"

        do! cmdResult.Task :> Task
      | None -> Logger.build "No Fable configuration provided, skipping fable"

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

      let buildConfig = { buildConfig with externals = excludes |> Some }

      do!
        Task.WhenAll(
          buildFiles ResourceType.JS jsFiles buildConfig,
          buildFiles ResourceType.CSS cssFiles buildConfig
        )
        :> Task

      let opts = EnumerationOptions()
      opts.RecurseSubdirectories <- true

      let getDirectories (map: Map<string, string>) =
        let root = Environment.CurrentDirectory

        let totalPaths =
          [| for key in map.Keys do
               yield!
                 Directory.EnumerateFiles(Path.GetFullPath(key), "*.*", opts)
                 |> Seq.map (fun s -> s, s) |]
        // FIXME: This thing is not funny to use, but at least in the meantime will work
        // Once the new build pipeline is in, this should disappear
        let includedFiles =
          [| for (path, target) in totalPaths do
               for copy in copyIncludes do
                 let copy, target =
                   match copy.Split("->") with
                   | [| origin; target |] -> origin.Trim(), target.Trim()
                   | [| origin |] -> origin.Trim(), target.Trim()
                   | _ -> copy.Trim(), copy.Trim()

                 let ext =
                   let copy = copy.Replace('/', Path.DirectorySeparatorChar)

                   if copy.StartsWith "." then
                     copy.Substring(2)
                   else
                     copy

                 if path.Contains(ext) then
                   yield path, target |]


        let excludedFiles =
          totalPaths
          |> Array.filter (fun ((path, _)) ->
            copyExcludes
            |> List.exists (fun ext -> path.Contains(ext))
            |> not)

        [| yield! excludedFiles
           yield! includedFiles |]
        |> Array.Parallel.iter (fun (origin, target) ->
          let target = Path.GetFullPath target
          let posPath = target.Replace(root, $"{outDir}")

          let print =
            if origin <> target then
              $"[blue]{origin}[/] -> [yellow]{target}[/]"
            else
              $"[blue]{origin}[/]"

          Logger.log ($"{print} -> [green]{posPath}[/]", escape = false)

          try
            Path.GetDirectoryName posPath
            |> Directory.CreateDirectory
            |> ignore
          with
          | _ -> ()

          File.Copy(origin, posPath))

      Logger.log $"Copying Files to out directory"

      devServer.mountDirectories
      |> Option.map getDirectories
      |> ignore

      let cssFiles =
        [ for jsFile in jsFiles do
            let name = (Path.GetFileName jsFile).Replace(".js", ".css")

            let dirName =
              (Path.GetDirectoryName jsFile)
                .Split(Path.DirectorySeparatorChar)
              |> Seq.last

            $"./{dirName}/{name}".Replace("\\", "/") ]

      Logger.log "Adding CSS Files to index.html"
      do! insertMapAndCopy cssFiles config
      Logger.log "Build finished."
    }
