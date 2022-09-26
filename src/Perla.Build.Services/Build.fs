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

  [<RequireQualifiedAccess>]
  type private EntryPoint =
    | Physical of physicalRelativePath: string
    | VirtualPath of virtualRelativePath: string * physicalRelativePath: string

    member this.RelativePath =
      match this with
      | Physical p
      | VirtualPath (_, p) -> p

  let private resolveVirtualFile (config: PerlaConfig) entryPath : EntryPoint =
    let resolveFor entryPath physicalFolder virtualFolder =
      let split (v: string) =
        v.Split(
          [| "./"; ".\\"; "/"; "\\" |],
          StringSplitOptions.RemoveEmptyEntries
        )
        |> List.ofArray

      let physicalParts = split physicalFolder
      let virtualParts = split virtualFolder

      let entryFileName, entryParts =
        match List.rev (split entryPath) with
        | [] -> failwith "input does not contain a file"
        | fileName :: rest -> fileName, List.rev rest

      if virtualParts.IsEmpty then
        // The physical folder is mount on the root.
        Path.Combine(
          [| yield! physicalParts; yield! entryParts; yield entryFileName |]
        )
      else
        // Detect how many parts of the entry parts are matching with the virtual path
        // Example: entry: ./src/js/App.js
        // Mapping: "./out": "/src"
        // This means that one part of the entry matches with the mapping.
        // And that "js/App/js" should exists in the "out"
        let rec visit state entryParts virtualParts : int =
          match entryParts, virtualParts with
          | [], _ -> state
          | iHead :: iRest, vHead :: vRest ->
            if iHead = vHead then
              visit (state + 1) iRest vRest
            else
              state
          | _ -> state

        let virtualMatches = visit 0 entryParts virtualParts
        let inputParts = List.skip virtualMatches entryParts

        Path.Combine(
          [| yield! physicalParts; yield! inputParts; yield entryFileName |]
        )

    let mountDir =
      config.devServer
      |> Option.bind (fun devServer -> devServer.mountDirectories)
      |> Option.map Map.toList

    match mountDir with
    | None ->
      failwith
        $"The entry path {entryPath} could not be resolved on disk and the configuration doesn't contain mountDirectories."
    | Some mounts ->
      let result =
        mounts
        |> List.map (fun (p, v) -> resolveFor entryPath p v)
        |> List.tryFind File.Exists

      match result with
      | None ->
        failwith
          $"The entry path {entryPath} could not be resolved on disk, nor could it be found in one of the mountDirectories."
      | Some p -> EntryPoint.VirtualPath(entryPath, p)

  let private getEntryPoints
    (workingDirectory: string)
    (type': ResourceType)
    (config: PerlaConfig)
    : EntryPoint list =
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

      src

    els
    |> Seq.map (fun element ->
      let value = getPathFromAttribute element
      let fullPath = Path.Combine(workingDirectory, value)

      if File.Exists fullPath then
        // The found entryPoint is pointing to a file on disk
        EntryPoint.Physical value
      else
        // The found entryPoint does not exist, the devServer might resolve it by a mounted folder.
        resolveVirtualFile config value)
    |> Seq.toList

  let private insertMapAndCopy jsFiles cssFiles config =
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
      [ for file: string in cssFiles do
          let file = Path.ChangeExtension(file, ".css")

          let style = doc.CreateElement("link")
          style.SetAttribute("rel", "stylesheet")
          style.SetAttribute("href", file)
          style ]

    let script = doc.CreateElement("script")
    script.SetAttribute("type", "importmap")

    doc.Body.QuerySelectorAll("[data-entry-point][type=module]")
    |> Seq.iter (fun el ->
      match el.GetAttribute("src") |> Option.ofObj with
      | Some src -> el.SetAttribute("src", Path.ChangeExtension(src, ".js"))
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

        let virtualEntries =
          jsFiles
          |> List.choose (function
            | EntryPoint.VirtualPath (v, p) -> Some(v, p)
            | EntryPoint.Physical _ -> None)

        for v, p in virtualEntries do
          let element =
            doc.Body.QuerySelector(
              $"[data-entry-point][type=module][src='{v}']"
            )

          if not (isNull element) then
            element.Attributes["src"].Value <- p.Replace("\\", "/")

        doc.Head.AppendChild script |> ignore
        let content = doc.ToHtml()

        File.WriteAllText($"{outDir}/{indexFile}", content)
      | Error err -> Logger.build ("Failed to get or create lock file", err)
    }

  let private buildFiles
    (workingDirectory: string)
    (type': ResourceType)
    (files: EntryPoint seq)
    (config: BuildConfig)
    =
    task {
      if files |> Seq.length > 0 then
        let entrypoints =
          files
          |> Seq.map (fun e -> Path.Combine(workingDirectory, e.RelativePath))
          |> String.concat " "

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
          lock.imports |> Map.toSeq |> Seq.map (fun (key, _) -> key)

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
        paths.excludes |> Option.defaultValue (BuildConfig.DefaultExcludes())

    let copyIncludes =
      match buildConfig.copyPaths with
      | None -> Seq.empty
      | Some paths -> paths.includes |> Option.defaultValue Seq.empty

    let emitEnvFile = defaultArg buildConfig.emitEnvFile true

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
      with ex ->
        ()

      Directory.CreateDirectory(outDir) |> ignore

      let pwd = Directory.GetCurrentDirectory()
      let jsFiles = getEntryPoints pwd ResourceType.JS config
      let cssFiles = getEntryPoints pwd ResourceType.CSS config
      let! excludes = getExcludes buildConfig

      let excludes =
        if emitEnvFile then
          let envPath =
            config.devServer
            |> Option.map (fun c -> c.envPath)
            |> Option.flatten
            |> Option.defaultValue "/env.js"

          excludes |> Seq.append (seq { envPath })
        else
          excludes

      let buildConfig = { buildConfig with externals = excludes |> Some }

      do!
        Task.WhenAll(
          buildFiles pwd ResourceType.JS jsFiles buildConfig,
          buildFiles pwd ResourceType.CSS cssFiles buildConfig
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

                   if copy.StartsWith "." then copy.Substring(2) else copy

                 if path.Contains(ext) then
                   yield path, target |]


        let excludedFiles =
          totalPaths
          |> Array.filter (fun ((path, _)) ->
            copyExcludes |> Seq.exists (fun ext -> path.Contains(ext)) |> not)

        [| yield! excludedFiles; yield! includedFiles |]
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
            Path.GetDirectoryName posPath |> Directory.CreateDirectory |> ignore
          with _ ->
            ()

          File.Copy(origin, posPath))

      Logger.log $"Copying Files to out directory"

      devServer.mountDirectories |> Option.map getDirectories |> ignore

      let cssFiles =
        [ for jsFile in jsFiles do
            let name =
              (Path.GetFileName jsFile.RelativePath).Replace(".js", ".css")

            let dirName =
              (Path.GetDirectoryName jsFile.RelativePath)
                .Split(Path.DirectorySeparatorChar)
              |> Seq.last

            $"./{dirName}/{name}".Replace("\\", "/") ]

      Logger.log "Adding CSS Files to index.html"
      do! insertMapAndCopy jsFiles cssFiles config

      let envPath =
        config.devServer
        |> Option.map (fun c -> c.envPath)
        |> Option.flatten
        |> Option.defaultValue "/env.js"

      let envPath = envPath.Substring(1)

      match emitEnvFile, Fs.getPerlaEnvContent () with
      | true, Some content ->
        Logger.log $"Generating perla env file "

        let envPath = Path.Combine(outDir, envPath)

        try
          Directory.CreateDirectory(Path.GetDirectoryName(envPath)) |> ignore
        with ex ->
#if DEBUG
          Logger.log (ex.Message, ex)
#else
          Logger.log "Couldn't create the directory for perla env file"
#endif
        try
          File.WriteAllText(envPath, content)
        with ex ->
#if DEBUG
          Logger.log (ex.Message, ex)
#else
          Logger.log "Couldn't create the perla env file"
#endif
      | false, _
      | _, None -> ()

      Logger.log "Build finished."
    }
