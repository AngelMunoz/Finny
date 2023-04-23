module Perla.Database

#nowarn "3391"

open System
open System.Linq.Expressions
open LiteDB

open FSharp.UMX

open Perla
open Perla.Units
open Perla.FileSystem


[<AllowNullLiteral; Class>]
type PerlaCheck() =

  [<BsonId>]
  member val CheckId: ObjectId = ObjectId.NewObjectId() with get, set

  member val Name: string = String.Empty with get, set

  member val IsDone: bool = false with get, set

  member val CreatedAt: DateTime = DateTime.Now with get, set

  member val UpdatedAt: Nullable<DateTime> = Nullable() with get, set

let Database =
  lazy
    (try
      UMX.untag FileSystem.Database
      |> IO.Path.GetDirectoryName
      |> IO.Directory.CreateDirectory
      |> ignore
     with ex ->
       ()

     new LiteDatabase($"Filename='{FileSystem.Database}';Connection='shared'"))

[<RequireQualifiedAccess>]
module PerlaCheck =

  let Collection =
    lazy
      (let database = Database.Value
       let checks = database.GetCollection<PerlaCheck>()

       checks.EnsureIndex((fun check -> check.Name), true) |> ignore
       checks.EnsureIndex(fun check -> check.IsDone) |> ignore
       checks)

  [<Literal>]
  let SetupCheckName = "SetupCheck"

  [<Literal>]
  let EsbuildCheckPrefix = "Esbuild:Version:"

  [<Literal>]
  let TemplatesCheckName = "TemplatesCheck"


[<Class; Sealed>]
type Checks =

  static member IsSetupPresent() : bool =
    let collection = PerlaCheck.Collection.Value
    let checkName = PerlaCheck.SetupCheckName
    collection.Exists(fun check -> check.Name = checkName && check.IsDone)

  static member SaveSetup() : ObjectId =
    let collection = PerlaCheck.Collection.Value

    match
      collection.FindOne(fun check -> check.Name = PerlaCheck.SetupCheckName)
      |> Option.ofObj
    with
    | Some found -> found.CheckId
    | None ->
      let check = PerlaCheck(Name = PerlaCheck.SetupCheckName, IsDone = true)
      collection.Insert(check)


  static member IsEsbuildBinPresent(version: string<Semver>) : bool =
    let checkName = $"{PerlaCheck.EsbuildCheckPrefix}{version}"
    let collection = PerlaCheck.Collection.Value
    collection.Exists(fun check -> check.Name = checkName && check.IsDone)

  static member SaveEsbuildBinPresent(version: string<Semver>) : ObjectId =
    let collection = PerlaCheck.Collection.Value
    let checkName = $"{PerlaCheck.EsbuildCheckPrefix}{version}"

    match
      collection.FindOne(fun check -> check.Name = checkName && check.IsDone)
      |> Option.ofObj
    with
    | Some found -> found.CheckId
    | None ->
      let check = PerlaCheck(Name = checkName, IsDone = true)
      collection.Insert(check)


  static member AreTemplatesPresent() : bool =
    let checkName = PerlaCheck.TemplatesCheckName
    let collection = PerlaCheck.Collection.Value

    collection.Exists(fun check -> check.Name = checkName && check.IsDone)

  static member SaveTemplatesPresent() : ObjectId =
    let checkName = PerlaCheck.TemplatesCheckName
    let collection = PerlaCheck.Collection.Value

    match
      collection.FindOne(fun check -> check.Name = checkName) |> Option.ofObj
    with
    | Some found -> found.CheckId
    | None ->
      let check = PerlaCheck(Name = checkName, IsDone = true)
      collection.Insert(check)
