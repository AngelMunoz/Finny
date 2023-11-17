namespace Perla.Tests

open System
open Xunit
open Xunit.Sdk

open FSharp.UMX
open Perla.Units
open Perla.Environment

module Env =
  open System.Collections

  type EnvironmentFake =

    static member GetEnvironmentVariables() =
      let dict = new Collections.Generic.Dictionary<string, string>()
      dict.Add("PERLA_Client", "Client")
      dict.Add("PERLASRV_Server", "Server")
      dict.Add("OtherNotAvailable", "other-not-available")
      dict :> IDictionary

  type FileFake =
    static member ReadAllLines path =
      match path with
      | "mixed" -> [|
          "PERLA_MixedClient=Client"
          "PERLASRV_MixedServer=Server"
          "OtherNotAvailable=other-not-available"
        |]
      | "client" -> [|
          "PERLA_Client=Client"
          "OtherNotAvailable=other-not-available"
        |]
      | "server" -> [|
          "PERLASRV_Server=Server"
          "OtherNotAvailable=other-not-available"
        |]
      | _ -> [||]


  [<Fact>]
  let ``LoadEnvFiles can load env vars from various files`` () =
    let found =
      EnvLoader.LoadEnvFiles<FileFake>(
        [ UMX.tag "mixed"; UMX.tag "client"; UMX.tag "server" ]
      )

    Assert.Equal(4, found.Length)

  [<Fact>]
  let ``LoadEnvFiles categorizes the env vars correctly`` () =
    let found = EnvLoader.LoadEnvFiles<FileFake>([ UMX.tag "mixed" ])

    match found with
    | [ mixedClient; mixedServer ] ->
      Assert.Equal("MixedClient", mixedClient.Name)
      Assert.Equal("Client", mixedClient.Value)
      Assert.Equal(EnvVarTarget.Client, mixedClient.Target)

      Assert.Equal(
        (EnvVarOrigin.File(UMX.tag<SystemPath> "")),
        mixedClient.Origin
      )

      Assert.Equal("MixedServer", mixedServer.Name)
      Assert.Equal("Server", mixedServer.Value)
      Assert.Equal(EnvVarTarget.PerlaServer, mixedServer.Target)

      Assert.Equal(
        (EnvVarOrigin.File(UMX.tag<SystemPath> "")),
        mixedServer.Origin
      )

    | _ -> Assert.Fail("Expected 2 env vars")

  [<Fact>]
  let ``LoadFromSystem can load env vars from the system`` () =
    let found = EnvLoader.LoadFromSystem<EnvironmentFake>()

    Assert.Equal(2, found.Length)

  [<Fact>]
  let ``LoadFromSystem categorizes the env vars correctly`` () =
    let found = EnvLoader.LoadFromSystem<EnvironmentFake>()

    match found with
    | [ client; server ] ->
      Assert.Equal("Client", client.Value)
      Assert.Equal(EnvVarTarget.Client, client.Target)
      Assert.Equal(EnvVarOrigin.System, client.Origin)

      Assert.Equal("Server", server.Value)
      Assert.Equal(EnvVarTarget.PerlaServer, server.Target)
      Assert.Equal(EnvVarOrigin.System, server.Origin)

    | _ -> Assert.Fail("Expected 2 env vars")
