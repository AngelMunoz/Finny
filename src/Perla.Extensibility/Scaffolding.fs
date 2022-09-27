module Perla.Lib.Extensibility.Scaffolding

open Perla.Lib.Fsi

let getConfigurationFromScript content =
  use session = Fsi.getSession ()

  session.EvalInteractionNonThrowing(content) |> ignore

  match session.TryFindBoundValue Constants.ScaffoldConfiguration with
  | Some bound -> Some bound.Value.ReflectionValue
  | None -> None
