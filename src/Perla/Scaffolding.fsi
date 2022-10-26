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
          username: string
          repository: string
          branch: string
          path: string<SystemPath>
          createdAt: DateTime
          updatedAt: Nullable<DateTime> }

        member ToFullName: string
        member ToFullNameWithBranch: string
        static member DefaultTemplatesRepository: string * string * string

    [<RequireQualifiedAccess>]
    type TemplateSearchKind =
        | Id of ObjectId
        | Username of name: string
        | Repository of repository: string
        | FullName of username: string * repository: string

    [<Class>]
    type Templates =
        static member List: unit -> PerlaTemplateRepository list
        static member Add: user: string * repository: string * branch: string -> Task<ObjectId>
        /// <summary>
        /// Checks if the the repository with given a name in the form of
        /// Username/Repository
        /// exists
        /// </summary>
        /// <param name="name">Full name of the template in the Username/Repository scheme</param>
        static member Exists: name: TemplateSearchKind -> bool
        /// <summary>
        /// Checks if the the repository with given a name in the form of
        /// Username/Repository
        /// exists
        /// </summary>
        /// <param name="name">Full name of the template in the Username/Repository scheme</param>
        static member FindOne: name: TemplateSearchKind -> PerlaTemplateRepository option
        static member Update: template: PerlaTemplateRepository -> Task<bool>
        static member Delete: searchKind: TemplateSearchKind -> bool
