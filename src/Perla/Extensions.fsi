[<AutoOpen>]
module Extensions

[<RequireQualifiedAccess>]
module Result =
  val inline requireVSome: error: 'b -> value: 'a voption -> Result<'a, 'b>
