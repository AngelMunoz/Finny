module Perla.Tests.Json

open System
open Xunit
open Xunit.Sdk

open Perla.Json
open Thoth.Json.Net
open FSharp.UMX
open Perla.Types
open Perla.PackageManager.Types

[<Fact>]
let ``PerlaDecoder Should Decode from an empty object`` () =
  match Decode.fromString ConfigDecoders.PerlaDecoder "{}" with
  | Ok decoded ->

    Assert.True decoded.index.IsNone
    Assert.True decoded.runConfiguration.IsNone
    Assert.True decoded.provider.IsNone
    Assert.True decoded.build.IsNone
    Assert.True decoded.devServer.IsNone
    Assert.True decoded.fable.IsNone
    Assert.True decoded.esbuild.IsNone
    Assert.True decoded.mountDirectories.IsNone
    Assert.True decoded.enableEnv.IsNone
    Assert.True decoded.envPath.IsNone
    Assert.True decoded.dependencies.IsNone
    Assert.True decoded.devDependencies.IsNone

  | Error err -> Assert.Fail $"Decoder couldn't decode due: {err}"


[<Fact>]
let ``Json.FromConfig Should Decode from an empty object`` () =
  match Json.FromConfigFile "{}" with
  | Ok decoded ->

    Assert.True decoded.index.IsNone
    Assert.True decoded.runConfiguration.IsNone
    Assert.True decoded.provider.IsNone
    Assert.True decoded.build.IsNone
    Assert.True decoded.devServer.IsNone
    Assert.True decoded.fable.IsNone
    Assert.True decoded.esbuild.IsNone
    Assert.True decoded.mountDirectories.IsNone
    Assert.True decoded.enableEnv.IsNone
    Assert.True decoded.envPath.IsNone
    Assert.True decoded.dependencies.IsNone
    Assert.True decoded.devDependencies.IsNone

  | Error err -> Assert.Fail $"Decoder couldn't decode due: {err}"

[<Fact>]
let ``PerlaDecoder Should Decode from a complete object`` () =
  let json =
    """
{
  "index": "index.html",
  "runConfiguration": "dev",
  "provider": "jspm",
  "mountDirectories": {
    "/src": "./src"
  },
  "enableEnv": true,
  "envPath": "/my-path/env.js",
  "dependencies": [{ "name": "lit", "version": "2.4.0" }],
  "devDependencies": []
}
"""

  match Decode.fromString ConfigDecoders.PerlaDecoder json with
  | Ok decoded ->
    match decoded.index with
    | None -> Assert.Fail "index should have a value"
    | Some index -> Assert.Equal("index.html", UMX.untag index)

    match decoded.runConfiguration with
    | None -> Assert.Fail "runConfiguration should have a value"
    | Some runConfiguration ->
      Assert.Equal(RunConfiguration.Development, runConfiguration)

    match decoded.provider with
    | None -> Assert.Fail "provider should have a value"
    | Some provider -> Assert.Equal(Provider.Jspm, provider)

    match decoded.mountDirectories with
    | None -> Assert.Fail "mountDirectories should have a value"
    | Some mountDirectories ->
      Assert.NotEmpty(mountDirectories)

      mountDirectories
      |> Map.tryFindKey (fun k _ -> k = (UMX.tag "/src"))
      |> Option.isSome
      |> Assert.True

    match decoded.enableEnv with
    | None -> Assert.Fail "enableEnv should have a value"
    | Some enableEnv -> Assert.True enableEnv

    match decoded.enableEnv with
    | None -> Assert.Fail "enableEnv should have a value"
    | Some enableEnv -> Assert.True enableEnv

    match decoded.envPath with
    | None -> Assert.Fail "envPath should have a value"
    | Some envPath -> Assert.Equal("/my-path/env.js", envPath)

    match decoded.dependencies with
    | None -> Assert.Fail "dependencies should have a value"
    | Some dependencies ->
      let actual = Assert.Single(dependencies)

      let expected: Dependency = {
        name = "lit"
        version = Some "2.4.0"
        alias = None
      }

      Assert.Equal(expected, actual)

    match decoded.devDependencies with
    | None -> Assert.Fail "devDependencies should have a value"
    | Some devDependencies -> Assert.Empty(devDependencies)

  | Error err -> Assert.Fail $"Decoder couldn't decode due: {err}"

[<Fact>]
let ``PerlaDecoder Should Decode Fable options`` () =
  let json =
    """
{ "fable": {
    "project": "./src/App.fsproj",
    "extension": ".jsx",
    "sourceMaps": false,
    "outDir": "../fable-bin"
  }
}
"""

  match Decode.fromString ConfigDecoders.PerlaDecoder json with
  | Ok { fable = Some fable } ->
    Assert.Equal(
      "./src/App.fsproj",
      fable.project |> Option.map UMX.untag |> Option.defaultValue "bad value"
    )

    Assert.Equal(
      ".jsx",
      fable.extension |> Option.map UMX.untag |> Option.defaultValue "bad value"
    )

    Assert.False(fable.sourceMaps |> Option.defaultValue true)

    Assert.Equal(
      "../fable-bin",
      fable.outDir |> Option.map UMX.untag |> Option.defaultValue "bad value"
    )
  | Ok { fable = None } ->
    Assert.Fail $"Fable is empty when it must have a value"
  | Error err -> Assert.Fail $"Decoder couldn't decode due: {err}"


[<Fact>]
let ``PerlaDecoder Should Decode DevServer options`` () =
  let json =
    """
{ "devServer": {
    "port": 5000,
    "host": "localhost",
    "liveReload": false,
    "useSSL": false,
    "proxy": {
      "/v1/api/{**catch-all}": "http://localhost:5000/api"
    }
  }
}
"""

  match Decode.fromString ConfigDecoders.PerlaDecoder json with
  | Ok { devServer = Some devServer } ->
    Assert.Equal(
      5000,
      devServer.port |> Option.map UMX.untag |> Option.defaultValue 0000
    )

    Assert.Equal(
      "localhost",
      devServer.host |> Option.map UMX.untag |> Option.defaultValue "bad value"
    )

    Assert.False(devServer.liveReload |> Option.defaultValue true)
    Assert.False(devServer.useSSL |> Option.defaultValue true)

    devServer.proxy
    |> Option.map (fun map ->
      map
      |> Map.tryFindKey (fun k _ -> k = "/v1/api/{**catch-all}")
      |> Option.isSome
      |> Assert.True)
    |> Option.defaultWith (fun _ ->
      Assert.Fail "Proxy Should have at least one key")
  | Ok { devServer = None } ->
    Assert.Fail $"DevServer is empty when it must have a value"
  | Error err -> Assert.Fail $"Decoder couldn't decode due: {err}"

[<Fact>]
let ``PerlaDecoder Should Decode Build options`` () =
  let json =
    """
{ "build": {
    "includes": ["docs/images/**/*.png"],
    "excludes": [],
    "outDir": "../dist",
    "emitEnvFile": true
  }
}
"""

  match Decode.fromString ConfigDecoders.PerlaDecoder json with
  | Ok { build = Some build } ->
    match build.includes with
    | None -> Assert.Fail "Includes should have a value"
    | Some includes ->
      let glob = Assert.Single includes
      Assert.Equal("docs/images/**/*.png", glob)

    match build.excludes with
    | None -> Assert.Fail "dependencies should have a value"
    | Some excludes -> Assert.Empty(excludes)

    Assert.Equal(
      "../dist",
      build.outDir |> Option.map UMX.untag |> Option.defaultValue "bad value"
    )

    Assert.True(build.emitEnvFile |> Option.defaultValue false)
  | Ok { build = None } ->
    Assert.Fail $"DevServer is empty when it must have a value"
  | Error err -> Assert.Fail $"Decoder couldn't decode due: {err}"


[<Fact>]
let ``PerlaDecoder Should Decode Esbuild options`` () =
  let json =
    """
{ "esbuild": {
    "esBuildPath": "/mnt/c/esbuild",
    "version": "0.17.15",
    "ecmaVersion": "es2017",
    "minify": true,
    "injects": [],
    "externals": ["react"],
    "fileLoaders": {},
    "jsxAutomatic": true,
    "jsxImportSource": "preact"
  }
}
"""

  match Decode.fromString ConfigDecoders.PerlaDecoder json with
  | Ok { esbuild = Some esbuild } ->
    Assert.Equal(
      "/mnt/c/esbuild",
      esbuild.esBuildPath
      |> Option.map UMX.untag
      |> Option.defaultValue "bad-value"
    )

    Assert.Equal(
      "0.17.15",
      esbuild.version |> Option.map UMX.untag |> Option.defaultValue "bad-value"
    )

    Assert.Equal(
      "es2017",
      esbuild.ecmaVersion
      |> Option.map UMX.untag
      |> Option.defaultValue "bad-value"
    )

    Assert.True(esbuild.minify |> Option.defaultValue false)

    match esbuild.injects with
    | None -> Assert.Fail "Includes should have a value"
    | Some injects -> Assert.Empty injects

    match esbuild.fileLoaders with
    | None -> Assert.Fail "Includes should have a value"
    | Some fileLoaders -> Assert.Empty fileLoaders

    match esbuild.externals with
    | None -> Assert.Fail "Includes should have a value"
    | Some externals ->
      let react = Assert.Single externals
      Assert.Equal("react", react)

    Assert.Equal(
      true,
      esbuild.jsxAutomatic
      |> Option.defaultWith (fun _ -> failwith "jsxAutomatic is not present")
    )

    Assert.Equal(
      "preact",
      esbuild.jsxImportSource |> Option.defaultValue "bad value"
    )

  | Ok { esbuild = None } ->
    Assert.Fail $"DevServer is empty when it must have a value"
  | Error err -> Assert.Fail $"Decoder couldn't decode due: {err}"
