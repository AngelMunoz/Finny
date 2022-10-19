namespace Perla.Fable

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices

open CliWrap

open Perla.Types


[<Class>]
type Fable =
    static member FablePid: int option
    static member Stop: unit -> unit
    static member Start:
        config: FableConfig *
        isWatch: bool *
        ?stdout: (string -> unit) *
        ?stderr: (string -> unit) *
        [<Optional>] ?cancellationToken: CancellationToken ->
            Task<CommandResult>
