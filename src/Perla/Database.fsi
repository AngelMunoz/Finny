module Perla.Database

open System
open LiteDB
open FSharp.UMX
open Perla.Units

val Database: Lazy<LiteDatabase>

[<AllowNullLiteral; Class>]
type PerlaCheck =

  [<BsonId>]
  member CheckId: ObjectId

  member Name: string

  member IsDone: bool

  member CreatedAt: DateTime

  member UpdatedAt: Nullable<DateTime>

[<Class; Sealed>]
type Checks =

  static member IsSetupPresent: unit -> bool
  static member SaveSetup: unit -> ObjectId

  static member IsEsbuildBinPresent: version: string<Semver> -> bool
  static member SaveEsbuildBinPresent: version: string<Semver> -> ObjectId

  static member AreTemplatesPresent: unit -> bool
  static member SaveTemplatesPresent: unit -> ObjectId
