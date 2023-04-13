namespace Perla

open System.Threading.Tasks
open Perla.Types
open Perla.PackageManager.Types
open System.Runtime.InteropServices

module Dependencies =
  val Search: name: string * page: int -> Task<unit>
  val Show: name: string -> Task<unit>

[<Class>]
type Dependencies =
  static member Add:
    package: string *
    map: ImportMap *
    provider: Provider *
    [<Optional>] ?runConfig: RunConfiguration ->
      Task<Result<ImportMap, string>>

  static member Restore:
    package: string *
    ?provider: Provider *
    [<Optional>] ?runConfig: RunConfiguration ->
      Task<Result<ImportMap, string>>

  static member Restore:
    packages: seq<string> *
    [<Optional>] ?provider: Provider *
    [<Optional>] ?runConfig: RunConfiguration ->
      Task<Result<ImportMap, string>>

  static member GetMapAndDependencies:
    packages: seq<string> *
    [<Optional>] ?provider: Provider *
    [<Optional>] ?runConfig: RunConfiguration ->
      Task<Result<string seq * ImportMap, string>>

  static member GetMapAndDependencies:
    map: ImportMap *
    [<Optional>] ?provider: Provider *
    [<Optional>] ?runConfig: RunConfiguration ->
      Task<Result<string seq * ImportMap, string>>

  static member Remove:
    package: string *
    map: ImportMap *
    provider: Provider *
    [<Optional>] ?runConfig: RunConfiguration ->
      Task<Result<ImportMap, string>>

  static member SwitchProvider:
    map: ImportMap *
    provider: Provider *
    [<Optional>] ?runConfig: RunConfiguration ->
      Task<Result<ImportMap, string>>

  static member LocateDependenciesFromMapAndConfig:
    importMap: ImportMap * config: PerlaConfig ->
      (Dependency seq * Dependency seq)
