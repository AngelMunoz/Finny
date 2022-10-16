namespace Perla.Build

open System
open System.IO
open System.Net.Http
open System.Threading.Tasks

open AngleSharp
open AngleSharp.Html.Parser

open Perla
open Perla.Types
open Perla.Units
open Perla.Json
open Perla.FileSystem
open Perla.VirtualFs
open Perla.Logger
open Perla.Esbuild
open Fake.IO.Globbing
open FSharp.UMX

[<RequireQualifiedAccess>]
type ResourceType =
  | JS
  | CSS

  member this.AsString() =
    match this with
    | JS -> "JS"
    | CSS -> "CSS"


[<RequireQualifiedAccess>]
module Build =

  [<RequireQualifiedAccess>]
  type EntryPoint =
    | Physical of physicalRelativePath: string
    | VirtualPath of virtualRelativePath: string * physicalRelativePath: string

    member this.RelativePath =
      match this with
      | Physical p
      | VirtualPath (_, p) -> p

  let resolveVirtualFile (config: PerlaConfig) entryPath : EntryPoint =
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

    let mounts =
      config.mountDirectories |> Map.toList

    let result =
      mounts
      |> List.map (fun (p, v) -> resolveFor entryPath $"{p}" $"{v}")
      |> List.tryFind File.Exists

    match result with
    | None ->
      failwith
        $"The entry path {entryPath} could not be resolved on disk, nor could it be found in one of the mountDirectories."
    | Some p -> EntryPoint.VirtualPath(entryPath, p)

  let getEntryPoints
    (workingDirectory: string<SystemPath>)
    (type': ResourceType)
    (config: PerlaConfig)
    : EntryPoint list =
    let context = BrowsingContext.New(Configuration.Default)

    let content = UMX.untag config.index |> Path.GetFullPath |> File.ReadAllText

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
      let fullPath = Path.Combine(UMX.untag workingDirectory, value)

      if File.Exists fullPath then
        // The found entryPoint is pointing to a file on disk
        EntryPoint.Physical value
      else
        // The found entryPoint does not exist, the devServer might resolve it by a mounted folder.
        resolveVirtualFile config value)
    |> Seq.toList

  let insertMapAndCopy jsFiles cssFiles config =
    let context = BrowsingContext.New(Configuration.Default)

    let content = UMX.untag config.index |> Path.GetFullPath |> File.ReadAllText

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

    let map = FileSystem.ImportMap()
    script.TextContent <- Json.ToText(map, true)

    for style in styles do
      doc.Head.AppendChild(style) |> ignore

    let virtualEntries =
      jsFiles
      |> List.choose (function
        | EntryPoint.VirtualPath (v, p) -> Some(v, p)
        | EntryPoint.Physical _ -> None)

    for v, p in virtualEntries do
      let element =
        doc.Body.QuerySelector($"[data-entry-point][type=module][src='{v}']")

      if not (isNull element) then
        element.Attributes["src"].Value <- p.Replace("\\", "/")

    doc.Head.AppendChild script |> ignore
    let content = doc.ToHtml()

    File.WriteAllText($"{config.build.outDir}/{config.index}", content)

  let buildFiles
    (workingDirectory: string<SystemPath>)
    (type': ResourceType)
    (files: EntryPoint seq)
    (externals: string seq)
    (config: PerlaConfig)
    =
    task {
      if files |> Seq.length > 0 then
        let entrypoints =
          files
          |> Seq.map (fun e -> Path.Combine(UMX.untag workingDirectory, e.RelativePath))
          |> String.concat " "

        let cmd =
          match type' with
          | ResourceType.JS ->
            let tsk = Esbuild.ProcessJS(entrypoints, config.esbuild, config.build.outDir, externals)
            tsk.ExecuteAsync()
          | ResourceType.CSS ->
            let tsk = Esbuild.ProcessCss(entrypoints, config.esbuild, config.build.outDir)
            tsk.ExecuteAsync()

        Logger.log($"Starting esbuild with pid: [{cmd.ProcessId}]", target=Build)

        return! cmd.Task :> Task
      else
        Logger.log($"No Entrypoints for {type'.AsString()} found in index.html", target=Build)
    }

  let getExternals (config: PerlaConfig) =
    let dependencies =
      match config.runConfiguration with
      | RunConfiguration.Production ->
          config.dependencies
      | RunConfiguration.Development ->
          config.devDependencies
    seq {
      for dependency in dependencies do
        dependency.name
        if dependency.alias.IsSome then
          dependency.alias.Value
      if config.enableEnv && config.build.emitEnvFile then
        UMX.untag config.envPath
      yield! config.esbuild.externals
    }

  let copyGlobs (config: BuildConfig) =
    let cwd = FileSystem.CurrentWorkingDirectory() |> UMX.untag
    let outDir = UMX.untag config.outDir
    let filesToCopy: LazyGlobbingPattern =
      { BaseDirectory = UMX.untag cwd; Includes = config.includes |> Seq.toList; Excludes = config.excludes |> Seq.toList; }
    Logger.log $"Copying Files to out directory"
    filesToCopy
    |> Seq.toArray
    |> Array.iter (fun file ->
      try
        Path.GetDirectoryName file |> Directory.CreateDirectory |> ignore
      with _ ->
        ()
      File.Copy(file, file.Replace(cwd, outDir)))

type Build =

  static member Execute (config: PerlaConfig) =
    task {
      Logger.log("Mounting Virtual File System", target=PrefixKind.Build)
      do! VirtualFileSystem.Mount config
      let pwd =  VirtualFileSystem.CopyToDisk() |> UMX.tag<SystemPath>
      Logger.log($"Copying Processed files to {pwd}", target=PrefixKind.Build)

      Logger.log("Resolving JS and CSS files", target=PrefixKind.Build)
      let jsFiles = Build.getEntryPoints pwd ResourceType.JS config
      let cssFiles = Build.getEntryPoints pwd ResourceType.CSS config
      let externals = Build.getExternals config

      Logger.log("Running Esbuild on finalized files", target=PrefixKind.Build)
      do!
        Task.WhenAll(
          Build.buildFiles pwd ResourceType.JS jsFiles externals config,
          Build.buildFiles pwd ResourceType.CSS cssFiles externals config
        )
        :> Task
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
      Build.insertMapAndCopy jsFiles cssFiles config

      let envPath = (UMX.untag config.envPath).Substring(1)

      match config.enableEnv && config.build.emitEnvFile, Env.GetEnvContent() with
      | true, Some content ->
        Logger.log $"Generating perla env file "

        let envPath = Path.Combine(UMX.untag config.build.outDir, envPath)

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

      Build.copyGlobs config.build

      Logger.log("Build finished.", target=PrefixKind.Build)
    }
