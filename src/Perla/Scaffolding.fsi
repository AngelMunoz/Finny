namespace Perla

open System
open System.Threading.Tasks
open LiteDB
open FSharp.UMX
open Perla.Types

module Scaffolding =
  open Units

  [<Literal>]
  val ScaffoldConfiguration: string = "TemplateConfiguration"

  val getConfigurationFromScript: content: string -> obj option

  [<CLIMutable>]
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

    member ToFullName: string
    member ToFullNameWithBranch: string
    static member DefaultTemplatesRepository: string * string * string

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

  [<RequireQualifiedAccess>]
  type TemplateSearchKind =
    | Id of ObjectId
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


  [<Class>]
  type Templates =
    static member ListRepositories: unit -> PerlaTemplateRepository list
    static member ListTemplateItems: unit -> TemplateItem list

    static member Add:
      user: string * repository: string * branch: string ->
        Task<Result<ObjectId, string>>

    /// <summary>
    /// Checks if the the repository with given a name in the form of
    /// Username/Repository
    /// exists
    /// </summary>
    /// <param name="name">Full name of the template in the Username/Repository scheme</param>
    static member FindOne:
      name: TemplateSearchKind -> PerlaTemplateRepository option

    static member FindTemplateItems:
      searchParams: QuickAccessSearch -> TemplateItem list

    static member Update:
      template: PerlaTemplateRepository -> Task<Result<bool, string>>

    static member Delete: searchKind: TemplateSearchKind -> bool

    static member GetTemplateScriptContent:
      scriptKind: TemplateScriptKind -> obj option
