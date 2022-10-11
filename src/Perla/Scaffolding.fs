namespace Perla

open System
open LiteDB

open Flurl.Http

open Perla.FileSystem

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

  [<Struct; RequireQualifiedAccess>]
  type NameKind =
    | Name of name: string
    | FullName of fullName: string

  let private templatesdb = lazy (new LiteDatabase(FileSystem.Database))

  let private repositories =
    lazy
      (let database = templatesdb.Value
       let repo = database.GetCollection<PerlaTemplateRepository>()

       repo.EnsureIndex(fun template -> template.fullName) |> ignore

       repo.EnsureIndex(fun template -> template.name) |> ignore
       repo)

  let downloadAndExtract repo =
    task {
      let url =
        $"https://github.com/{repo.fullName}/archive/refs/heads/{repo.branch}.zip"

      use! stream = url.GetStreamAsync()
      FileSystem.ExtractTemplateZip repo.fullName stream
    }



  type Templates =

    static member List() =
      repositories.Value.FindAll() |> Seq.toList

    static member Add(name, fullName, branch, path) =
      task {
        let template =
          { _id = ObjectId.NewObjectId()
            name = name
            fullName = fullName
            branch = branch
            path = path
            createdAt = DateTime.Now
            updatedAt = Nullable() }

        do! downloadAndExtract template

        return repositories.Value.Insert(template).AsObjectId
      }

    /// <summary>
    /// Checks if the the repository with given a name in the form of
    /// Username/Repository
    /// exists
    /// </summary>
    /// <param name="name">Full name of the template in the Username/Repository scheme</param>
    static member Exists(name: NameKind) =
      repositories.Value.Exists(fun clamRepo ->
        match name with
        | NameKind.Name name -> clamRepo.name = name
        | NameKind.FullName name -> clamRepo.fullName = name)

    /// <summary>
    /// Checks if the the repository with given a name in the form of
    /// Username/Repository
    /// exists
    /// </summary>
    /// <param name="name">Full name of the template in the Username/Repository scheme</param>
    static member FindOne(name: NameKind) : PerlaTemplateRepository option =
      repositories.Value.FindOne(fun clamRepo ->
        match name with
        | NameKind.Name name -> clamRepo.name = name
        | NameKind.FullName name -> clamRepo.fullName = name)
      |> box
      |> Option.ofObj
      |> Option.map unbox

    static member FindOne(id: ObjectId) : PerlaTemplateRepository option =
      repositories.Value.FindById(id) |> box |> Option.ofObj |> Option.map unbox

    static member Update(template: PerlaTemplateRepository) =
      task {
        match Templates.FindOne(NameKind.FullName template.fullName) with
        | Some repo ->
          let updated = { repo with updatedAt = Nullable(DateTime.Now) }
          templatesdb.Value.BeginTrans() |> ignore

          try
            do! downloadAndExtract updated

            return
              repositories.Value.Update(repo._id, updated)
              && templatesdb.Value.Commit()
          with _ ->
            templatesdb.Value.Rollback() |> ignore
            return false
        | None -> return false
      }

    static member Delete(fullName: string) =
      match Templates.FindOne(NameKind.FullName fullName) with
      | Some template ->
        templatesdb.Value.BeginTrans() |> ignore
        FileSystem.RemoveTemplateDirectory template.fullName
        repositories.Value.Delete(template._id) |> ignore
        templatesdb.Value.Commit()
      | None -> false
