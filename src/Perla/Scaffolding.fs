namespace Perla

open System
open LiteDB

open Flurl.Http

open Perla.FileSystem

open FSharp.UMX

module Scaffolding =
  open Units

  [<CLIMutable>]
  type PerlaTemplateRepository =
    { _id: ObjectId
      username: string
      repository: string
      branch: string
      path: string<SystemPath>
      createdAt: DateTime
      updatedAt: Nullable<DateTime> }

    member this.ToFullName =

      $"{this.username}/{this.repository}"

    member this.ToFullNameWithBranch =
      $"{this.username}/{this.repository}:{this.branch}"

    static member DefaultTemplatesRepository =
      Constants.Default_Templates_Repository.Split("/")
      |> function
        | [| username; repository |] ->
          username, repository, Constants.Default_Templates_Repository_Branch
        | _ ->
          "AngelMunoz",
          "perla-templates",
          Constants.Default_Templates_Repository_Branch

  [<RequireQualifiedAccess>]
  type TemplateSearchKind =
    | Id of ObjectId
    | Username of name: string
    | Repository of repository: string
    | FullName of username: string * repository: string

  let private templatesdb =
    lazy
      (new LiteDatabase(
        $"Filename='{UMX.untag FileSystem.Database}';Connection='shared'"
      ))

  let private repositories =
    lazy
      (let database = templatesdb.Value
       let repo = database.GetCollection<PerlaTemplateRepository>()

       repo.EnsureIndex(fun template -> template.username) |> ignore
       repo.EnsureIndex(fun template -> template.repository) |> ignore
       repo)

  let downloadAndExtract (user: string, repository: string, branch: string) =
    task {
      let url =
        $"https://github.com/{user}/{repository}/archive/refs/heads/{branch}.zip"

      use! stream = url.GetStreamAsync()

      return FileSystem.ExtractTemplateZip (user, repository, branch) stream
    }

  type Templates =

    static member List() =
      repositories.Value.FindAll() |> Seq.toList

    static member Add(user, repository, branch) =
      task {
        let! path = downloadAndExtract (user, repository, branch)

        let template =
          { _id = ObjectId.NewObjectId()
            username = user
            repository = repository
            branch = branch
            path = path
            createdAt = DateTime.Now
            updatedAt = Nullable() }

        return repositories.Value.Insert(template).AsObjectId
      }

    /// <summary>
    /// Checks if the the repository with given a name in the form of
    /// Username/Repository
    /// exists
    /// </summary>
    /// <param name="name">Full name of the template in the Username/Repository scheme</param>
    static member Exists(name: TemplateSearchKind) =
      repositories.Value.Exists(fun tplRepository ->
        match name with
        | TemplateSearchKind.Id id -> tplRepository._id = id
        | TemplateSearchKind.Username name -> tplRepository.username = name
        | TemplateSearchKind.Repository name -> tplRepository.repository = name
        | TemplateSearchKind.FullName (username, repository) ->
          tplRepository.username = username
          && tplRepository.repository = repository)

    /// <summary>
    /// Checks if the the repository with given a name in the form of
    /// Username/Repository
    /// exists
    /// </summary>
    /// <param name="name">Full name of the template in the Username/Repository scheme</param>
    static member FindOne
      (name: TemplateSearchKind)
      : PerlaTemplateRepository option =
      let templates = repositories.Value

      let result =
        match name with
        | TemplateSearchKind.Id id -> templates.FindById(id)
        | TemplateSearchKind.Username username ->
          templates
            .Query()
            .Where(fun tplRepo -> tplRepo.username = username)
            .SingleOrDefault()
        | TemplateSearchKind.Repository repository ->
          templates
            .Query()
            .Where(fun tplRepo -> tplRepo.repository = repository)
            .SingleOrDefault()
        | TemplateSearchKind.FullName (username, repository) ->
          templates
            .Query()
            .Where(fun tplRepo ->
              tplRepo.username = username && tplRepo.repository = repository)
            .SingleOrDefault()

      match box result with
      | :? PerlaTemplateRepository as repo -> Some repo
      | null
      | _ -> None

    static member Update(template: PerlaTemplateRepository) =
      task {
        match
          Templates.FindOne(
            TemplateSearchKind.FullName(template.username, template.repository)
          )
        with
        | Some repo ->
          let updated = { repo with updatedAt = Nullable(DateTime.Now) }

          try
            let! path =
              downloadAndExtract (
                updated.username,
                updated.repository,
                updated.branch
              )
              |> Async.AwaitTask

            let updated =
              repositories.Value.Update(repo._id, { updated with path = path })

            return updated
          with ex ->
            Logger.Logger.log ("We could not update the template", ex = ex)
            templatesdb.Value.Rollback() |> ignore
            return false
        | None -> return false
      }

    static member Delete(searchKind) =
      match Templates.FindOne(searchKind) with
      | Some template ->
        FileSystem.RemoveTemplateDirectory(template.path)

        repositories.Value.Delete(template._id)
      | None -> false
