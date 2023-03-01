module Tests.TestingUtils

open Fable.Core

type IExpect =

  abstract ``to``: IExpect

  abstract ``not``: IExpect

  abstract exist: IExpect

  abstract ``include``<'T> : 'T -> unit

[<ImportMember "@esm-bundle/chai">]
let expect (value: obj) : IExpect = jsNative

[<Erase>]
type Testing =

  [<Emit("describe($0, $1)")>]
  static member Describe(name: string, callback: unit -> unit) : unit = jsNative

  [<Emit("describe($0, $1)")>]
  static member Describe
    (
      name: string,
      callback: unit -> JS.Promise<unit>
    ) : unit =
    jsNative

  [<Emit("it($0, $1)")>]
  static member It(name: string, callback: unit -> unit) : unit = jsNative

  [<Emit("it($0, $1)")>]
  static member It(name: string, callback: unit -> JS.Promise<unit>) : unit =
    jsNative
