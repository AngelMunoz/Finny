namespace Perla.Tests

open Xunit

open Perla
open Perla.Types
open Perla.Logger
open Perla.PackageManager.Types

open FsToolkit.ErrorHandling

module Dependencies =

  [<Literal>]
  let LitVersion = "2.0.0"

  [<Literal>]
  let LodashVersion = "4.17.21"

  [<Literal>]
  let LitName = "lit"

  [<Literal>]
  let LodashName = "lodash"

  [<Literal>]
  let LodashStaticUrl = "https://ga.jspm.io/npm:lodash@4.17.21/lodash.js"

  let VersionedLit = $"{LitName}@{LitVersion}"

  let VersionedLodash = $"{LodashName}@{LodashVersion}"

  let DefaultResponseValues r =
    r
    |> TaskResult.teeError (fun err ->
      Logger.log (
        $"Add should respect and add the requested pacakge failed: {err}"
      ))
    |> TaskResult.defaultValue { imports = Map.empty; scopes = None }

  let AssertLodash (importMap: ImportMap, provider: Provider) =
    match
      importMap.imports
      |> Map.tryPick (fun k v -> if k = LodashName then Some v else None)
    with
    | Some url ->
      match ExtractDependencyInfoFromUrl url with
      | ValueSome(resultProvider, name, version) ->
        Assert.Equal(provider, resultProvider)
        Assert.Equal(LodashName, name)
        Assert.Equal(LodashVersion, version)
      | ValueNone ->
        Assert.Fail(
          $"Failed to extract Dependency information from URL, its structure may have changed: {url}"
        )
    | None -> Assert.Fail "The Entry is not in the dictionary"

  let AssertLit (importMap: ImportMap, provider: Provider) =
    match
      importMap.imports
      |> Map.tryPick (fun k v -> if k = LitName then Some v else None)
    with
    | Some url ->
      match ExtractDependencyInfoFromUrl url with
      | ValueSome(resultProvider, name, version) ->
        Assert.Equal(provider, resultProvider)
        Assert.Equal(LitName, name)
        Assert.Equal(LitVersion, version)
      | ValueNone ->
        Assert.Fail(
          $"Failed to extract Dependency information from URL, its structure may have changed: {url}"
        )
    | None -> Assert.Fail "The Entry is not in the dictionary"

  let AssertJQuery (importMap: ImportMap, provider: Provider) =
    match
      importMap.imports
      |> Map.tryPick (fun k v -> if k = "jquery" then Some v else None)
    with
    | Some url ->
      match ExtractDependencyInfoFromUrl url with
      | ValueSome(resultProvider, name, version) ->
        Assert.Equal(provider, resultProvider)
        Assert.Equal("jquery", name)
        Assert.Equal("3.6.1", version)
      | ValueNone ->
        Assert.Fail(
          $"Failed to extract Dependency information from URL, its structure may have changed: {url}"
        )
    | None -> Assert.Fail "The Entry is not in the dictionary"

  [<Fact>]
  let ``Add should respect and add the requested pacakge`` () =
    task {
      let! result =
        Dependencies.Add(
          VersionedLit,
          { imports = Map.empty; scopes = None },
          Provider.Jspm
        )
        |> DefaultResponseValues

      AssertLit(result, Provider.Jspm)

    }

  [<Fact>]
  let ``Add should respect an existing import map and add the requested packages``
    ()
    =
    task {
      let! result =
        Dependencies.Add(
          VersionedLodash,
          { imports =
              [ "lit",
                "https://cdn.skypack.dev/pin/lit@v2.0.0-B36tAUEdI9Ino7UGfR7h/mode=imports,min/optimized/lit.js" ]
              |> Map.ofList
            scopes = None },
          Provider.Unpkg
        )
        |> DefaultResponseValues

      AssertLit(result, Provider.Skypack)
      AssertLodash(result, Provider.Unpkg)
    }

  [<Fact>]
  let ``Restore should generate a new import map from the requested pacakge``
    ()
    =
    task {
      let! result =
        Dependencies.Restore(VersionedLodash, Provider.Unpkg)
        |> DefaultResponseValues

      AssertLodash(result, Provider.Unpkg)
    }

  [<Fact>]
  let ``Restore should generate a new import map from the requested pacakges``
    ()
    =
    task {
      let! result =
        Dependencies.Restore(
          [ VersionedLodash; VersionedLit ],
          Provider.Jsdelivr
        )
        |> DefaultResponseValues

      AssertLit(result, Provider.Jsdelivr)
      AssertLodash(result, Provider.Jsdelivr)
    }

  [<Fact>]
  let ``GetMapAndDependencies should generate an import map and give a static dependency sequence``
    ()
    =
    task {
      let! (dependencies, importMap) =
        Dependencies.GetMapAndDependencies([ VersionedLodash ], Provider.Jspm)
        |> TaskResult.teeError (fun err ->
          Logger.log (
            $"Add should respect and add the requested pacakge failed: {err}"
          ))
        |> TaskResult.defaultValue ([], { imports = Map.empty; scopes = None })

      let url = Assert.Single(dependencies)
      Assert.Equal(LodashStaticUrl, url)
      AssertLodash(importMap, Provider.Jspm)
    }

  [<Fact>]
  let ``Remove should respect the existing import map and remove the requested dependency``
    ()
    =
    task {
      let importMap =
        { imports =
            [ "jquery", "https://ga.jspm.io/npm:jquery@3.6.1/dist/jquery.js"
              "lodash", "https://ga.jspm.io/npm:lodash@4.17.21/lodash.js"
              "lit",
              "https://cdn.skypack.dev/pin/lit@v2.0.0-B36tAUEdI9Ino7UGfR7h/mode=imports,min/optimized/lit.js" ]
            |> Map.ofList
          scopes = None }

      let! result =
        Dependencies.Remove("jquery", importMap, Provider.Jspm)
        |> DefaultResponseValues

      AssertLodash(result, Provider.Jspm)
      AssertLit(result, Provider.Skypack)
    }


  [<Fact>]
  let ``SwitchProvider should respect the existing import map and change where the dependencies come from``
    ()
    =
    task {
      let importMap =
        { imports =
            [ "jquery", "https://ga.jspm.io/npm:jquery@3.6.1/dist/jquery.js"
              "lodash", "https://ga.jspm.io/npm:lodash@4.17.21/lodash.js"
              "lit",
              "https://cdn.skypack.dev/pin/lit@v2.0.0-B36tAUEdI9Ino7UGfR7h/mode=imports,min/optimized/lit.js" ]
            |> Map.ofList
          scopes = None }

      let! result =
        Dependencies.SwitchProvider(importMap, Provider.Unpkg)
        |> DefaultResponseValues

      AssertLit(result, Provider.Unpkg)
      AssertLodash(result, Provider.Unpkg)
      AssertJQuery(result, Provider.Unpkg)
    }

  [<Fact>]
  let ``LocateDependenciesFromMapAndConfig should not grab dependencies from import map if they don't exist in the configuration``
    ()
    =
    let importMap =
      { imports =
          [ "jquery", "https://ga.jspm.io/npm:jquery@3.6.1/dist/jquery.js"
            "lodash", "https://ga.jspm.io/npm:lodash@4.17.21/lodash.js"
            "lit",
            "https://cdn.skypack.dev/pin/lit@v2.0.0-B36tAUEdI9Ino7UGfR7h/mode=imports,min/optimized/lit.js" ]
          |> Map.ofList
        scopes = None }

    let config =
      { Configuration.Defaults.PerlaConfig with
          dependencies =
            [ { name = LitName
                version = Some LitVersion
                alias = None }
              { name = LodashName
                version = Some LodashVersion
                alias = None } ] }

    let dependencies, devDependencies =
      Dependencies.LocateDependenciesFromMapAndConfig(importMap, config)

    Assert.Contains(
      { name = LitName
        version = Some LitVersion
        alias = None },
      dependencies
    )

    Assert.Contains(
      { name = LodashName
        version = Some LodashVersion
        alias = None },
      dependencies
    )

    Assert.DoesNotContain(
      { name = "jquery"
        version = Some "3.6.1"
        alias = None },
      dependencies
    )

    Assert.Empty(devDependencies)

  [<Fact>]
  let ``LocateDependenciesFromMapAndConfig should grab dependencies from import map if they exist in the configuration``
    ()
    =
    let importMap =
      { imports =
          [ "jquery", "https://ga.jspm.io/npm:jquery@3.6.1/dist/jquery.js"
            "lodash", "https://ga.jspm.io/npm:lodash@4.17.21/lodash.js"
            "lit",
            "https://cdn.skypack.dev/pin/lit@v2.0.0-B36tAUEdI9Ino7UGfR7h/mode=imports,min/optimized/lit.js" ]
          |> Map.ofList
        scopes = None }

    let config =
      { Configuration.Defaults.PerlaConfig with
          dependencies =
            [ { name = LitName
                version = Some LitVersion
                alias = None }
              { name = LodashName
                version = Some LodashVersion
                alias = None } ]
          devDependencies =
            [ { name = "jquery"
                version = Some "3.6.1"
                alias = None } ] }

    let dependencies, devDependencies =
      Dependencies.LocateDependenciesFromMapAndConfig(importMap, config)

    Assert.Contains(
      { name = LitName
        version = Some LitVersion
        alias = None },
      dependencies
    )

    Assert.Contains(
      { name = LodashName
        version = Some LodashVersion
        alias = None },
      dependencies
    )

    Assert.Contains(
      { name = "jquery"
        version = Some "3.6.1"
        alias = None },
      devDependencies
    )

    Assert.DoesNotContain(
      { name = "jquery"
        version = Some "3.6.1"
        alias = None },
      dependencies
    )
