namespace Perla

open System
open System.Threading.Tasks
open LiteDB
open Flurl.Http
open Perla.FileSystem
open FSharp.UMX

module Scaffolding =
    open Units

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

        member AsString: string

    [<Struct; RequireQualifiedAccess>]
    type NameKind =
        | Name of name: string
        | FullName of fullName: string

    [<Class>]
    type Templates =
        static member List: unit -> PerlaTemplateRepository list
        static member Add: name: string * fullName: string * branch: string * path: string -> Task<ObjectId>
        /// <summary>
        /// Checks if the the repository with given a name in the form of
        /// Username/Repository
        /// exists
        /// </summary>
        /// <param name="name">Full name of the template in the Username/Repository scheme</param>
        static member Exists: name: NameKind -> bool
        /// <summary>
        /// Checks if the the repository with given a name in the form of
        /// Username/Repository
        /// exists
        /// </summary>
        /// <param name="name">Full name of the template in the Username/Repository scheme</param>
        static member FindOne: name: NameKind -> PerlaTemplateRepository option
        static member FindOne: id: ObjectId -> PerlaTemplateRepository option
        static member Update: template: PerlaTemplateRepository -> Task<bool>
        static member Delete: fullName: string -> bool
