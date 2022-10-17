module Tests

open System
open Xunit
open Xunit.Sdk


type Env() =
  do
    Environment.SetEnvironmentVariable("PERLA_currentEnv", "tests")
    Environment.SetEnvironmentVariable("PERLA_IAmSet", "yes")
    Environment.SetEnvironmentVariable("PERLA-NotAvailable", "not-available")

    Environment.SetEnvironmentVariable(
      "OtherNotAvailable",
      "other-not-available"
    )

  [<Fact>]
  member _.``GetEnvContent provides EnvVars with "PERLA_" prefix``() =
    let actual = Perla.Env.GetEnvContent()

    match actual with
    | Some actual ->
      let expected = "export const IAmSet = \"yes\";"
      Assert.True(actual.Contains(expected))
      let expected = "export const currentEnv = \"tests\";"
      Assert.True(actual.Contains(expected))
    | None ->
      raise (XunitException("Content is Empty when It should have data"))

  [<Fact>]
  member _.``GetEnvContent doesn't provide EnvVars without "PERLA_" prefix``() =
    let actual = Perla.Env.GetEnvContent()

    match actual with
    | Some actual ->
      let expected = "export const NotAvailable = \"not-available\";"
      Assert.False(actual.Contains(expected))
      let expected = "export const OtherNotAvailable = \"not-available\";"
      Assert.False(actual.Contains(expected))
    | None ->
      raise (XunitException("Content is Empty when It should have data"))



[<Fact>]
let ``My test`` () = Assert.True(true)
