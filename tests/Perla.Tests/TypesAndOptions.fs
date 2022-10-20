namespace Perla.Tests

open Xunit
open Perla.Types
open Perla.CliOptions

module Init =

  [<Fact>]
  let ``Init should be able to be converted from string`` () =
    Assert.Equal(Init.Full, Init.FromString "full")
    Assert.Equal(Init.Simple, Init.FromString "simple")
    Assert.Equal(Init.Simple, Init.FromString "3895746789345789cdsvdfsbthert")

module RunConfiguration =

  [<Fact>]
  let ``RunConfiguration should be able to be converted from string`` () =
    Assert.Equal(
      RunConfiguration.Production,
      RunConfiguration.FromString "prod"
    )

    Assert.Equal(
      RunConfiguration.Production,
      RunConfiguration.FromString "production"
    )

    Assert.Equal(
      RunConfiguration.Development,
      RunConfiguration.FromString "dev"
    )

    Assert.Equal(
      RunConfiguration.Development,
      RunConfiguration.FromString "development"
    )

    Assert.Equal(
      RunConfiguration.Development,
      RunConfiguration.FromString "r89yf23489hf249uhi"
    )

  [<Fact>]
  let ``RunConfiguration should be able to be converted to string`` () =
    Assert.Equal("production", RunConfiguration.Production.AsString)
    Assert.Equal("development", RunConfiguration.Development.AsString)

module Dependency =

  [<Fact>]
  let ``Dependency should be able to give a versioned string`` () =
    Assert.Equal(
      "lit@2.0.0",
      { name = "lit"
        version = ValueSome "2.0.0"
        alias = ValueNone }
        .AsVersionedString
    )

    Assert.Equal(
      "react",
      { name = "react"
        version = ValueNone
        alias = ValueNone }
        .AsVersionedString
    )

    Assert.Equal(
      "react@17.2.0",
      { name = "react"
        version = ValueSome "17.2.0"
        alias = ValueSome "react-17" }
        .AsVersionedString
    )
