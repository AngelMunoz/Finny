namespace Perla

open System.Threading.Tasks
open Perla.PackageManager.Types

module Dependencies =
    val Search: name: string * page: int -> Task<unit>
    val Show: name: string -> Task<unit>

[<Class>]
type Dependencies =
    static member inline Add: package: string * map: ImportMap * provider: Provider -> Task<Result<ImportMap, string>>
    static member inline Restore: package: string * ?provider: Provider -> Task<Result<ImportMap, string>>
    static member inline Restore: packages: seq<string> * ?provider: Provider -> Task<Result<ImportMap, string>>
    static member inline Remove:
        package: string * map: ImportMap * provider: Provider -> Task<Result<ImportMap, string>>
    static member inline SwitchProvider: map: ImportMap * provider: Provider -> Task<Result<ImportMap, string>>
