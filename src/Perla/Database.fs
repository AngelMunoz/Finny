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
    (new LiteDatabase($"Filename='{FileSystem.Database}';Connection='shared'"))

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

    collection
      .Query()
      .Where(fun check -> check.Name = checkName && check.IsDone)
      .Exists()

  static member SaveSetup() : ObjectId =
    let check = PerlaCheck(Name = PerlaCheck.SetupCheckName, IsDone = true)
    let collection = PerlaCheck.Collection.Value
    collection.Insert(check)


  static member IsEsbuildBinPresent(version: string<Semver>) : bool =
    let checkName = $"{PerlaCheck.EsbuildCheckPrefix}{version}"
    let collection = PerlaCheck.Collection.Value

    collection
      .Query()
      .Where(fun check -> check.Name = checkName && check.IsDone)
      .Exists()

  static member SaveEsbuildBinPresent(version: string<Semver>) : ObjectId =
    let checkName = $"{PerlaCheck.EsbuildCheckPrefix}{version}"
    let check = PerlaCheck(Name = checkName, IsDone = true)
    let collection = PerlaCheck.Collection.Value
    collection.Insert(check)


  static member AreTemplatesPresent() : bool =
    let checkName = PerlaCheck.TemplatesCheckName
    let collection = PerlaCheck.Collection.Value

    collection
      .Query()
      .Where(fun check -> check.Name = checkName && check.IsDone)
      .Exists()

  static member SaveTemplatesPresent() : ObjectId =
    let checkName = PerlaCheck.TemplatesCheckName
    let collection = PerlaCheck.Collection.Value

    let check =
      collection
        .Query()
        .Where(fun check -> check.Name = checkName)
        .ToEnumerable()
      |> Seq.tryHead
      |> Option.map (fun check ->
        check.UpdatedAt <- Nullable(DateTime.Now)
        check)
      |> Option.defaultValue (PerlaCheck(Name = checkName, IsDone = true))

    if collection.Upsert(check) then
      check.CheckId
    else
      failwith "Failed to save templates check"
