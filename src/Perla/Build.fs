namespace Perla.Build

open System.IO

open AngleSharp
open AngleSharp.Html.Dom

open Perla
open Perla.Types
open Perla.Units
open Perla.FileSystem

open Perla.PackageManager.Types

open Fake.IO.Globbing

open FSharp.UMX
open Spectre.Console

[<RequireQualifiedAccess>]
module Build =

  let insertCssFiles
    (
      document: IHtmlDocument,
      cssEntryPoints: string<ServerUrl> seq
    ) =
    for file in cssEntryPoints do
      let style = document.CreateElement("link")
      style.SetAttribute("rel", "stylesheet")
      style.SetAttribute("href", UMX.untag file)
      style |> document.Head.AppendChild |> ignore

  let insertModulePreloads (document: IHtmlDocument, staticDeps: string seq) =
    for dependencyUrl in staticDeps do
      let link = document.CreateElement("link")
      link.SetAttribute("rel", "modulepreload")
      link.SetAttribute("href", dependencyUrl)
      document.Head.AppendChild(link) |> ignore

  let insertImportMap (document: IHtmlDocument, importMap: ImportMap) =
    let script = document.CreateElement("script")
    script.SetAttribute("type", "importmap")
    script.TextContent <- importMap.ToJson()
    document.Head.AppendChild(script) |> ignore

  let insertJsFiles
    (
      document: IHtmlDocument,
      jsEntryPoints: string<ServerUrl> seq
    ) =
    for entryPoint in jsEntryPoints do
      let script = document.CreateElement("script")
      script.SetAttribute("type", "module")
      script.SetAttribute("src", UMX.untag entryPoint)
      document.Body.AppendChild(script) |> ignore


type Build =

  static member GetIndexFile
    (
      document: IHtmlDocument,
      cssPaths: string<ServerUrl> seq,
      jsPaths: string<ServerUrl> seq,
      importMap: ImportMap,
      ?staticDependencies: string seq,
      ?minify: bool
    ) =

    Build.insertCssFiles (document, cssPaths)

    // importmap needs to go first
    Build.insertImportMap (document, importMap)

    // if we have module preloads
    Build.insertModulePreloads (
      document,
      defaultArg staticDependencies Seq.empty
    )
    // remove any existing entry points, we don't need them at this point
    document.QuerySelectorAll("[data-entry-point][type=module]")
    |> Seq.iter (fun f -> f.Remove())

    document.QuerySelectorAll("[data-entry-point][rel=stylesheet]")
    |> Seq.iter (fun f -> f.Remove())

    // insert the resolved entry points which should match paths in mounted directories
    Build.insertJsFiles (document, jsPaths)

    match defaultArg minify false with
    | true -> document.Minify()
    | false -> document.ToHtml()

  static member GetEntryPoints(document: IHtmlDocument) =
    let css =
      document.QuerySelectorAll("[data-entry-point][rel=stylesheet]")
      |> Seq.choose (fun el -> el.Attributes["href"] |> Option.ofObj)
      |> Seq.map (fun el -> UMX.tag<ServerUrl> el.Value)

    let js =
      document.QuerySelectorAll("[data-entry-point][type=module]")
      |> Seq.choose (fun el -> el.Attributes["src"] |> Option.ofObj)
      |> Seq.map (fun el -> UMX.tag<ServerUrl> el.Value)

    css, js

  static member GetExternals(config: PerlaConfig) =
    let dependencies =
      match config.runConfiguration with
      | RunConfiguration.Production -> config.dependencies
      | RunConfiguration.Development ->
        [ yield! config.dependencies; yield! config.devDependencies ]

    seq {
      for dependency in dependencies do
        dependency.name

        if dependency.alias.IsSome then
          dependency.alias.Value

      if config.enableEnv && config.build.emitEnvFile then
        UMX.untag config.envPath

      yield! config.esbuild.externals
    }

  static member CopyGlobs(config: BuildConfig) =
    let cwd = FileSystem.CurrentWorkingDirectory() |> UMX.untag
    let outDir = UMX.untag config.outDir |> Path.GetFullPath

    let filesToCopy: LazyGlobbingPattern =
      { BaseDirectory = UMX.untag cwd
        Includes = config.includes |> Seq.toList
        Excludes = config.excludes |> Seq.toList }

    AnsiConsole
      .Progress()
      .Start(fun ctx ->
        let tsk =
          ctx.AddTask(
            "Copying Files to out directory",
            true,
            filesToCopy |> Seq.length |> float
          )

        filesToCopy
        |> Seq.toArray
        |> Array.iter (fun file ->
          tsk.Increment(1)
          let targetPath = file.Replace(cwd, outDir)

          try
            Path.GetDirectoryName targetPath
            |> Directory.CreateDirectory
            |> ignore
          with _ ->
            ()

          File.Copy(file, targetPath))

        tsk.StopTask())
