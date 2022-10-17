namespace Perla.Fable

open System.Threading.Tasks
open CliWrap
open Perla.Types


[<Class>]
type Fable =
    static member FablePid: int option
    static member Stop: unit -> unit
    static member Start:
        config: FableConfig * isWatch: bool * ?stdout: (string -> unit) * ?stderr: (string -> unit) ->
            Task<CommandResult>
