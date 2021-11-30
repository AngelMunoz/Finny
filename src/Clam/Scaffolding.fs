namespace Clam

open System
open System.Diagnostics
open System.IO
open System.IO.Compression
open Scriban
open FsHttp
open FsHttp.DslCE
open FsToolkit.ErrorHandling

open Clam.Types

module Scaffolding =

  let downloadRepo repo =
    let url =
      $"https://github.com/{repo.fullName}/archive/refs/heads/{repo.branch}.zip"

    Directory.CreateDirectory PathExt.TemplatesDirectory
    |> ignore

    try
      get url { send }
      |> Response.toBytes
      |> (fun bytes ->
        File.WriteAllBytes(
          Path.Combine(PathExt.TemplatesDirectory, $"{repo.name}.zip"),
          bytes
        ))

      Some repo
    with
    | ex -> None

  let unzipAndClean repo =
    match repo with
    | Some repo ->
      Directory.CreateDirectory PathExt.TemplatesDirectory
      |> ignore

      let username = (Directory.GetParent repo.path).Name

      try
        Directory.Delete(repo.path, true) |> ignore
      with
      | :? DirectoryNotFoundException -> printfn "Didn't delete Directory"

      let relativePath =
        Path.Join(repo.path, "../", "../")
        |> Path.GetFullPath

      let zipPath =
        Path.Combine(relativePath, $"{repo.name}.zip")
        |> Path.GetFullPath

      ZipFile.ExtractToDirectory(
        zipPath,
        Path.Combine(PathExt.TemplatesDirectory, username)
      )

      File.Delete(zipPath)
      Some repo
    | None -> None

  let private collectRepositoryFiles (path: string) =
    let foldFilesAndTemplates (files, templates) (next: string) =
      if next.Contains(".tpl.") then
        (files, next :: templates)
      else
        (next :: files, templates)

    let opts = EnumerationOptions()
    opts.RecurseSubdirectories <- true

    Directory.EnumerateFiles(path, "*.*", opts)
    |> Seq.filter (fun path -> not <| path.Contains(".fsx"))
    |> Seq.fold foldFilesAndTemplates (List.empty<string>, List.empty<string>)

  let private compileFiles (payload: obj option) (file: string) =
    let tpl = Template.Parse(file)
    tpl.Render(payload |> Option.toObj)

  let compileAndCopy (origin: string) (target: string) (payload: obj option) =
    let (files, templates) = collectRepositoryFiles origin

    let copyFiles () =
      files
      |> Array.ofList
      |> Array.Parallel.iter (fun file ->
        let target = file.Replace(origin, target)
        Directory.GetParent(target).Create()
        File.Copy(file, target, true))

    let copyTemplates () =
      templates
      |> Array.ofList
      |> Array.Parallel.iter (fun path ->
        let target = path.Replace(origin, target).Replace(".tpl", "")

        Directory.GetParent(target).Create()

        let content = File.ReadAllText(path) |> compileFiles payload

        File.WriteAllText(target, content))

    Directory.CreateDirectory(target) |> ignore
    copyFiles ()
    copyTemplates ()
