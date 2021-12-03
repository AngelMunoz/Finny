namespace Clam.Types

open System
open LiteDB

[<CLIMutable>]
type ClamRepo =
  { _id: ObjectId
    name: string
    fullName: string
    branch: string
    path: string
    createdAt: DateTime
    updatedAt: Nullable<DateTime> }

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

type NameParsingErrors =
  | MissingRepoName
  | WrongGithubFormat

  member this.AsString =
    match this with
    | MissingRepoName -> "The repository name is missing"
    | WrongGithubFormat -> "The repository name is not a valid github name"

type RepositoryOptions =
  { fullRepositoryName: string
    branch: string }

type ProjectOptions =
  { projectName: string
    templateName: string }
