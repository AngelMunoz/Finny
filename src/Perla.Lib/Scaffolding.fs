namespace Perla.Lib

open System.Threading.Tasks
open System.IO
open System.IO.Compression
open Scriban

open Flurl.Http
open FsToolkit.ErrorHandling

open Types
open Logger

module Scaffolding =

  let downloadRepo repo =
    task {
      let url =
        $"https://github.com/{repo.fullName}/archive/refs/heads/{repo.branch}.zip"

      Directory.CreateDirectory Path.TemplatesDirectory
      |> ignore

      try
        do!
          url.DownloadFileAsync(Path.TemplatesDirectory, $"{repo.name}.zip")
          :> Task

        return Some repo
      with
      | _ -> return None
    }

  let unzipAndClean (repo: Task<PerlaTemplateRepository option>) =
    task {
      let! repo = repo

      match repo with
      | Some repo ->
        Directory.CreateDirectory Path.TemplatesDirectory
        |> ignore

        let username = (Directory.GetParent repo.path).Name

        try
          Directory.Delete(repo.path, true) |> ignore
        with
        | :? DirectoryNotFoundException ->
          Logger.scaffold "Did not delete directory"

        let relativePath =
          Path.Join(repo.path, "../", "../")
          |> Path.GetFullPath

        let zipPath =
          Path.Combine(relativePath, $"{repo.name}.zip")
          |> Path.GetFullPath

        ZipFile.ExtractToDirectory(
          zipPath,
          Path.Combine(Path.TemplatesDirectory, username)
        )

        File.Delete(zipPath)
        return Some repo
      | None -> return None
    }

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
