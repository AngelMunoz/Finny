[<AutoOpen>]
module Extensions


[<RequireQualifiedAccess>]
module Result =
  let inline requireVSome error value =
    match value with
    | ValueSome v -> Ok v
    | ValueNone -> Error error
