namespace Perla.Fable

open System
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices

open CliWrap

open Perla.Types

[<RequireQualifiedAccess>]
type FableEvent =
  | Log of string
  | ErrLog of string
  | WaitingForChanges


[<Class>]
type Fable =

  /// Use this method to run a one-off fable execution
  static member Start:
    config: FableConfig *
    [<Optional>] ?stdout: (string -> unit) *
    [<Optional>] ?stderr: (string -> unit) *
    [<Optional>] ?cancellationToken: CancellationToken ->
      Task<CommandResult>

  /// Use this method to monitor fable stdout/stderr logs
  /// and get notice when a compilation finishes
  static member Observe:
    config: FableConfig *
    [<Optional>] ?isWatch: bool *
    [<Optional>] ?stdout: (string -> unit) *
    [<Optional>] ?stderr: (string -> unit) *
    [<Optional>] ?cancellationToken: CancellationToken ->
      IObservable<FableEvent>
