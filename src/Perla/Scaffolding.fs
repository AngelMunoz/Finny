namespace Perla

open System
open LiteDB

open FsHttp

open Perla.FileSystem

open FSharp.UMX
open Perla.Extensibility

open Perla.Json.TemplateDecoders
open Perla.Units
open Thoth.Json.Net


module Scaffolding =
  open Units
  open FsToolkit.ErrorHandling

  [<Literal>]
  let ScaffoldConfiguration = "TemplateConfiguration"

  let getConfigurationFromScript content =
    use session = Fsi.GetSession()

    session.EvalInteractionNonThrowing(content) |> ignore

    match session.TryFindBoundValue ScaffoldConfiguration with
    | Some bound -> Some bound.Value.ReflectionValue
    | None -> None

  [<CLIMutable>]
  type TemplateItem = {
    _id: ObjectId
    parent: ObjectId
    name: string
    group: string<TemplateGroup>
    shortName: string
    description: string option
    fullPath: string<SystemPath>
  }

  type TemplateConfigurationItem = {
    childId: ObjectId
    name: string
    shortName: string
    description: string
  }

  [<CLIMutable>]
  type PerlaTemplateRepository = {
    _id: ObjectId
    username: string
    repository: string
    branch: string
    path: string<SystemPath>
    name: string
    description: string
    author: string
    license: string
    repositoryUrl: string
    group: string<RepositoryGroup>
    templates: TemplateConfigurationItem seq
    createdAt: DateTime
    updatedAt: Nullable<DateTime>
  } with

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
    | Group of group: string<RepositoryGroup>
    | Username of name: string
    | Repository of repository: string
    | FullName of username: string * repository: string

  [<RequireQualifiedAccess>]
  type QuickAccessSearch =
    | Id of ObjectId
    | Name of string
    | Group of string<TemplateGroup>
    | ShortName of string
    | Parent of ObjectId

  [<RequireQualifiedAccess; Struct>]
  type TemplateScriptKind =
    | Template of template: TemplateItem
    | Repository of repository: PerlaTemplateRepository

  let Database =
    lazy
      (new LiteDatabase(
        $"Filename='{UMX.untag FileSystem.Database}';Connection='shared'"
      ))

  let RepositoriesCol =
    lazy
      (let database = Database.Value
       let repo = database.GetCollection<PerlaTemplateRepository>()

       repo.EnsureIndex(fun template -> template.username) |> ignore
       repo.EnsureIndex(fun template -> template.repository) |> ignore
       repo.EnsureIndex((fun template -> template.group), true) |> ignore
       repo)

  let TemplatesCol =
    lazy
      (let database = Database.Value
       let repo = database.GetCollection<TemplateItem>()
       repo.EnsureIndex(fun template -> template.parent) |> ignore
       repo.EnsureIndex((fun template -> template.group), true) |> ignore
       repo.EnsureIndex(fun template -> template.name) |> ignore
       repo.EnsureIndex(fun template -> template.shortName) |> ignore
       repo)

  let downloadAndExtract (user: string, repository: string, branch: string) = task {
    let! url =
      http {
        GET
          $"https://github.com/{user}/{repository}/archive/refs/heads/{branch}.zip"
      }
      |> Request.sendTAsync


    use! stream = url |> Response.toStreamTAsync

    return FileSystem.ExtractTemplateZip (user, repository, branch) stream
  }

  let buildTemplateItems
    (templateItems: DecodedTemplateConfigItem seq)
    (parentPath: string<SystemPath>)
    (parentGroup: string<RepositoryGroup>)
    parentId
    =
    templateItems
    |> Seq.map (fun templateItem -> {
      _id = ObjectId.NewObjectId()
      parent = parentId
      name = templateItem.name
      group = UMX.tag $"{parentGroup}.{templateItem.id}"
      shortName = templateItem.shortName
      description = templateItem.description
      fullPath =
        System.IO.Path.Combine(
          UMX.untag parentPath,
          UMX.untag templateItem.path
        )
        |> UMX.tag
    })

  let buildTemplateConfigurationItems (templates: TemplateItem seq) =
    templates
    |> Seq.map (fun item -> {
      childId = item._id
      name = item.name
      shortName = item.shortName
      description =
        item.description |> Option.defaultValue "No Description Provided"
    })

  let readTemplateScriptContents (path: string) =
    try
      System.IO.File.ReadAllText(
        System.IO.Path.Combine(path, Constants.TemplatingScriptName)
      )
      |> Some
    with _ ->
      None

  let buildTemplateRepository
    (options:
      {|
        id: ObjectId
        user: string
        repository: string
        branch: string
        path: string<SystemPath>
        name: string
        author: string
        license: string
        repositoryUrl: string
        group: string<RepositoryGroup>
        templates: seq<TemplateConfigurationItem>
        description: string
        updatedAt: Nullable<DateTime>
      |})
    =

    {
      _id = options.id
      username = options.user
      repository = options.repository
      branch = options.branch
      path = options.path
      createdAt = DateTime.Now
      updatedAt = options.updatedAt
      name = options.name
      description = options.description
      author = options.author
      license = options.license
      repositoryUrl = options.repositoryUrl
      group = options.group
      templates = options.templates
    }

  type Templates =

    static member ListRepositories() =
      RepositoriesCol.Value.FindAll() |> Seq.toList

    static member ListTemplateItems() =
      TemplatesCol.Value.FindAll() |> Seq.toList

    static member Add(user, repository, branch) = taskResult {
      let! result = downloadAndExtract (user, repository, branch)
      let path, config = result
      let! config = config
      let id = ObjectId.NewObjectId()

      let templateItems: TemplateItem seq =
        buildTemplateItems
          config.templates
          path
          (UMX.tag<RepositoryGroup> config.group)
          id

      let template =
        buildTemplateRepository {|
          id = id
          user = user
          repository = repository
          branch = branch
          path = path
          updatedAt = Nullable()
          name = config.name
          description =
            config.description |> Option.defaultValue "No description provided"
          author = config.author |> Option.defaultValue "No author provided"
          license = config.license |> Option.defaultValue "No license provided"
          repositoryUrl =
            config.repositoryUrl
            |> Option.defaultValue
              $"https://github.com/{user}/{repository}/tree/{branch}"
          group = UMX.tag<RepositoryGroup> config.group
          templates = buildTemplateConfigurationItems templateItems
        |}

      let parentId = RepositoriesCol.Value.Insert(template).AsObjectId

      TemplatesCol.Value.InsertBulk templateItems |> ignore

      return parentId
    }

    /// <summary>
    /// Checks if the the repository with given a name in the form of
    /// Username/Repository
    /// exists
    /// </summary>
    /// <param name="name">Full name of the template in the Username/Repository scheme</param>
    static member FindOne
      (name: TemplateSearchKind)
      : PerlaTemplateRepository option =
      let templates = RepositoriesCol.Value

      let result =
        match name with
        | TemplateSearchKind.Id id -> templates.FindById(id)
        | TemplateSearchKind.Username username ->
          templates
            .Query()
            .Where(fun tplRepo ->
              tplRepo.username.Equals(
                username,
                StringComparison.InvariantCultureIgnoreCase
              ))
            .SingleOrDefault()
        | TemplateSearchKind.Group group ->
          templates
            .Query()
            .Where(fun tplRepo ->
              (UMX.untag tplRepo.group)
                .Equals(
                  UMX.untag group,
                  StringComparison.InvariantCultureIgnoreCase
                ))
            .SingleOrDefault()
        | TemplateSearchKind.Repository repository ->
          templates
            .Query()
            .Where(fun tplRepo ->
              tplRepo.repository.Equals(
                repository,
                StringComparison.InvariantCultureIgnoreCase
              ))
            .SingleOrDefault()
        | TemplateSearchKind.FullName(username, repository) ->
          templates
            .Query()
            .Where(fun tplRepo ->
              tplRepo.username.Equals(
                username,
                StringComparison.InvariantCultureIgnoreCase
              )
              && tplRepo.repository.Equals(
                repository,
                StringComparison.InvariantCultureIgnoreCase
              ))
            .SingleOrDefault()

      Option.ofNull result

    static member FindTemplateItems(searchParams: QuickAccessSearch) =
      let templatesCol = TemplatesCol.Value

      let result =
        match searchParams with
        | QuickAccessSearch.Id id ->
          templatesCol.FindById(id)
          |> Option.ofNull
          |> Option.map (fun tpl -> [| tpl |])
          |> Option.defaultWith (fun _ -> Array.empty)

        | QuickAccessSearch.Name name ->
          templatesCol
            .Query()
            .Where(fun tpl ->
              tpl.name.Equals(
                name,
                StringComparison.InvariantCultureIgnoreCase
              ))
            .ToArray()
        | QuickAccessSearch.Group group ->
          templatesCol
            .Query()
            .Where(fun tpl ->
              (UMX.untag tpl.group)
                .Equals(
                  UMX.untag group,
                  StringComparison.InvariantCultureIgnoreCase
                ))
            .ToArray()
        | QuickAccessSearch.ShortName shortName ->
          templatesCol
            .Query()
            .Where(fun tpl ->
              tpl.shortName.Equals(
                shortName,
                StringComparison.InvariantCultureIgnoreCase
              ))
            .ToArray()
        | QuickAccessSearch.Parent id ->
          templatesCol.Query().Where(fun tpl -> tpl.parent.Equals(id)).ToArray()

      result |> List.ofArray

    static member Update(template: PerlaTemplateRepository) = taskResult {
      match
        Templates.FindOne(
          TemplateSearchKind.FullName(template.username, template.repository)
        )
      with
      | Some repo ->
        let updated = {
          repo with
              updatedAt = Nullable(DateTime.Now)
              branch = template.branch
        }

        try
          let! path =
            downloadAndExtract (
              updated.username,
              updated.repository,
              updated.branch
            )
            |> Async.AwaitTask

          let path, config = path
          let! config = config

          let templateItems: TemplateItem seq =
            buildTemplateItems
              config.templates
              repo.path
              (UMX.tag<RepositoryGroup> config.group)
              repo._id


          let updated =
            RepositoriesCol.Value.Update(
              repo._id,
              {
                updated with
                    path = path
                    name = config.name
                    description =
                      config.description
                      |> Option.defaultValue "No description provided"
                    author =
                      config.author |> Option.defaultValue "No author provided"
                    license =
                      config.license
                      |> Option.defaultValue "No license provided"
                    repositoryUrl =
                      config.repositoryUrl
                      |> Option.defaultValue
                        $"https://github.com/{updated.ToFullName}/tree/{updated.branch}"
                    group = UMX.tag<RepositoryGroup> config.group
                    templates = buildTemplateConfigurationItems templateItems
              }
            )

          if updated then
            // cleanup existing templates from the database
            TemplatesCol.Value.DeleteMany(fun template ->
              template.parent = repo._id)
            |> ignore

            // Insert the updated templates
            TemplatesCol.Value.InsertBulk templateItems |> ignore

          return updated
        with ex ->
          Logger.Logger.log ("We could not update the template", ex = ex)
          Database.Value.Rollback() |> ignore
          return false
      | None -> return false
    }

    static member Delete(searchKind) =
      match Templates.FindOne(searchKind: TemplateSearchKind) with
      | Some template ->
        FileSystem.RemoveTemplateDirectory(template.path)

        // remove the quick template access
        TemplatesCol.Value.DeleteMany(fun template ->
          template.parent = template._id)
        |> ignore

        RepositoriesCol.Value.Delete(template._id)
      | None -> false

    static member GetTemplateScriptContent(scriptKind: TemplateScriptKind) =
      let content =
        match scriptKind with
        | TemplateScriptKind.Template template ->
          template.fullPath |> UMX.untag |> readTemplateScriptContents
        | TemplateScriptKind.Repository repository ->
          repository.path |> UMX.untag |> readTemplateScriptContents

      content |> Option.map getConfigurationFromScript |> Option.flatten
