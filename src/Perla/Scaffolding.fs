namespace Perla

open System
open System.Threading.Tasks
open System.IO
open System.IO.Compression
open LiteDB
open Scriban

open Flurl.Http
open FsToolkit.ErrorHandling

open Perla.Logger


module Scaffolding =

  [<CLIMutable>]
  type PerlaTemplateRepository =
    { _id: ObjectId
      name: string
      fullName: string
      branch: string
      path: string
      createdAt: DateTime
      updatedAt: Nullable<DateTime> }

  type NameParsingErrors =
    | MissingRepoName
    | WrongGithubFormat

  type NameParsingErrors with

    member this.AsString =
      match this with
      | MissingRepoName -> "The repository name is missing"
      | WrongGithubFormat -> "The repository name is not a valid github name"

  type PerlaTemplateRepository with

    static member NewClamRepo
      (path: string)
      (name: string, fullName: string, branch: string)
      =
      { _id = ObjectId.NewObjectId()
        name = name
        fullName = fullName
        branch = branch
        path = path
        createdAt = DateTime.Now
        updatedAt = Nullable() }

  exception TemplateNotFoundException of string
  exception AddTemplateFailedException
  exception UpdateTemplateFailedException
  exception DeleteTemplateFailedException

  module Database =

    let clamRepos (database: ILiteDatabase) =
      let repo = database.GetCollection<PerlaTemplateRepository>()

      repo.EnsureIndex(fun clamRepo -> clamRepo.fullName) |> ignore

      repo.EnsureIndex(fun clamRepo -> clamRepo.name) |> ignore

      repo

    let listEntries () =
      use db = new LiteDatabase(Path.LocalDBPath)
      let clamRepos = clamRepos db
      clamRepos.FindAll() |> Seq.toList

    let createEntry (clamRepo: PerlaTemplateRepository option) =
      option {
        let! clamRepo = clamRepo
        use db = new LiteDatabase(Path.LocalDBPath)
        let clamRepos = clamRepos db
        let result = clamRepos.Insert(clamRepo)

        match result |> Option.ofObj with
        | Some _ -> return clamRepo
        | None -> return! None
      }

    /// <summary>
    /// Checks if the the repository with given a name in the form of
    /// Username/Repository
    /// exists
    /// <param name="fullName">Full name of the template in the Username/Repository scheme</param>
    /// </summary>
    let existsByFullName fullName =
      use db = new LiteDatabase(Path.LocalDBPath)
      let clamRepos = clamRepos db
      clamRepos.Exists(fun clamRepo -> clamRepo.fullName = fullName)

    /// <summary>
    /// Checks if the repository exists given a simple name
    /// <param name="name">Simple name of the repository (not including the GitHub owner)</param>
    /// </summary>
    let existsByName name =
      use db = new LiteDatabase(Path.LocalDBPath)
      let clamRepos = clamRepos db
      clamRepos.Exists(fun clamRepo -> clamRepo.name = name)

    /// <summary>
    /// Finds a repository using the name of the repository.
    /// <param name="name">Simple name of the repository (not including the GitHub owner)</param>
    /// </summary>
    let findByName name =
      use db = new LiteDatabase(Path.LocalDBPath)
      let clamRepos = clamRepos db

      clamRepos.FindOne(fun repo -> repo.name = name) :> obj
      |> Option.ofObj
      |> Option.map (fun o -> o :?> PerlaTemplateRepository)

    /// <summary>
    /// Finds a repository using the full name of the repository (ex. Username/Repository)
    /// <param name="fullName">Full name of the repository including the GitHub owner</param>
    /// </summary>
    let findByFullName fullName =
      use db = new LiteDatabase(Path.LocalDBPath)
      let clamRepos = clamRepos db

      clamRepos.FindOne(fun repo -> repo.fullName = fullName) :> obj
      |> Option.ofObj
      |> Option.map (fun o -> o :?> PerlaTemplateRepository)

    let updateByName name =
      match findByName name with
      | Some repo ->
        use db = new LiteDatabase(Path.LocalDBPath)
        let clamRepos = clamRepos db
        let repo = { repo with updatedAt = Nullable(DateTime.Now) }
        clamRepos.Update(BsonValue(repo._id), repo)
      | None -> false

    let updateEntry (repo: PerlaTemplateRepository option) =
      option {
        let! repo = repo
        use db = new LiteDatabase(Path.LocalDBPath)
        let clamRepos = clamRepos db
        let repo = { repo with updatedAt = Nullable(DateTime.Now) }
        return clamRepos.Update(BsonValue(repo._id), repo)
      }

    let deleteByFullName fullName =
      match findByFullName fullName with
      | Some repo ->
        use db = new LiteDatabase(Path.LocalDBPath)
        let clamRepos = clamRepos db
        clamRepos.Delete(BsonValue(repo._id))
      | None -> false

  let downloadRepo repo =
    task {
      let url =
        $"https://github.com/{repo.fullName}/archive/refs/heads/{repo.branch}.zip"

      Directory.CreateDirectory Path.TemplatesDirectory |> ignore

      try
        do!
          url.DownloadFileAsync(Path.TemplatesDirectory, $"{repo.name}.zip")
          :> Task

        return Some repo
      with _ ->
        return None
    }

  let unzipAndClean (repo: Task<PerlaTemplateRepository option>) =
    task {
      let! repo = repo

      match repo with
      | Some repo ->
        Directory.CreateDirectory Path.TemplatesDirectory |> ignore

        let username = (Directory.GetParent repo.path).Name

        try
          Directory.Delete(repo.path, true) |> ignore
        with :? DirectoryNotFoundException ->
          Logger.scaffold "Did not delete directory"

        let relativePath =
          Path.Join(repo.path, "../", "../") |> Path.GetFullPath

        let zipPath =
          Path.Combine(relativePath, $"{repo.name}.zip") |> Path.GetFullPath

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


  ///<summary>
  /// Gets the base templates directory (next to the perla binary)
  /// and appends the final path repository name
  /// </summary>
  let getPerlaRepositoryPath (repositoryName: string) (branch: string) =
    Path.Combine(Path.TemplatesDirectory, $"{repositoryName}-{branch}")
    |> Path.GetFullPath

  let getPerlaTemplatePath
    (repo: PerlaTemplateRepository)
    (child: string option)
    =
    match child with
    | Some child -> Path.Combine(repo.path, child)
    | None -> repo.path
    |> Path.GetFullPath

  let getPerlaTemplateTarget projectName =
    Path.Combine("./", projectName) |> Path.GetFullPath

  let removePerlaRepository (repository: PerlaTemplateRepository) =
    Directory.Delete(repository.path, true)

  let getPerlaTemplateScriptContent templatePath clamRepoPath =
    let readTemplateScript =
      try
        File.ReadAllText(Path.Combine(templatePath, "templating.fsx")) |> Some
      with _ ->
        None

    let readRepoScript () =
      try
        File.ReadAllText(Path.Combine(clamRepoPath, "templating.fsx")) |> Some
      with _ ->
        None

    readTemplateScript |> Option.orElseWith (fun () -> readRepoScript ())

  let getPerlaRepositoryChildren (repo: PerlaTemplateRepository) =
    DirectoryInfo(repo.path).GetDirectories()
