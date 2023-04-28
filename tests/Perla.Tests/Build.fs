namespace Perla.Tests

open Xunit
open AngleSharp
open AngleSharp.Html.Parser
open Perla.Build
open Perla.PackageManager.Types

open FSharp.UMX

module Build =
  let baseIndex =
    """<!DOCTYPE html>
<html>
  <head>
    <link
      rel="stylesheet"
      data-entry-point
      href="./src/app-styles.css"
    />
  </head>
  <body>
    <script data-entry-point type="module" src="./src/main.js"></script>
  </body>
</html>
"""

  [<Fact>]
  let ``GetIndexFile should add css links, js scripts, an import map without dependencies and not minified``
    ()
    =
    use browserCtx = new BrowsingContext()
    let parser = browserCtx.GetService<IHtmlParser>()
    let document = parser.ParseDocument baseIndex

    let map: ImportMap = {
      imports = [ "lit", "https://lit.dev" ] |> Map.ofList
      scopes = None
    }

    let result =
      Build.GetIndexFile(
        document,
        [ UMX.tag "./app-styles.css" ],
        [ UMX.tag "./src/main.js" ],
        map
      )
      |> parser.ParseDocument

    Assert.True(result.Head.ChildElementCount = 2)

    let style =
      Assert.Single(result.Head.QuerySelectorAll("link[rel=stylesheet]"))

    Assert.Equal("./app-styles.css", style.GetAttribute("href"))

    let map =
      Assert.Single(result.Head.QuerySelectorAll("script[type=importmap]"))

    Assert.Contains("lit", map.TextContent)
    Assert.Contains("https://lit.dev", map.TextContent)

    let mainScript =
      Assert.Single(result.Body.QuerySelectorAll("script[type=module]"))

    Assert.Equal("./src/main.js", mainScript.GetAttribute("src"))


  [<Fact>]
  let ``GetIndexFile should add module preload dependencies`` () =
    use browserCtx = new BrowsingContext()
    let parser = browserCtx.GetService<IHtmlParser>()
    let document = parser.ParseDocument baseIndex

    let map: ImportMap = {
      imports = [ "lit", "https://lit.dev" ] |> Map.ofList
      scopes = None
    }

    let result =
      Build.GetIndexFile(
        document,
        [],
        [],
        map,
        [ "https://lit.dev/index.js"; "https://reactjs.org/index.js" ]
      )
      |> parser.ParseDocument

    Assert.True(result.Head.ChildElementCount = 3)
    let preloads = result.Head.QuerySelectorAll("link[rel=modulepreload]")

    Assert.Contains(
      preloads,
      fun p -> p.GetAttribute("href") = "https://lit.dev/index.js"
    )

    Assert.Contains(
      preloads,
      fun p -> p.GetAttribute("href") = "https://reactjs.org/index.js"
    )


  [<Fact>]
  let ``GetEntryPoints has to gather all css/js elements with attribute 'data-entry-point'``
    ()
    =
    use browserCtx = new BrowsingContext()
    let parser = browserCtx.GetService<IHtmlParser>()
    let document = parser.ParseDocument baseIndex

    let css, js, _ = Build.GetEntryPoints(document)

    let url = Assert.Single(css)
    Assert.Equal("./src/app-styles.css", url)
    let url = Assert.Single(js)
    Assert.Equal("./src/main.js", url)

  [<Fact>]
  let ``GetExternals should bring all of the dependencies in the Perla Config``
    ()
    =
    let config = {
      Perla.Configuration.Defaults.PerlaConfig with
          esbuild = {
            Perla.Configuration.Defaults.EsbuildConfig with
                externals = [ "my-local-dep"; "external-dep" ]
          }
          dependencies = [
            {
              name = "lit"
              version = None
              alias = Some "lit-v1"
            }
            {
              name = "lit"
              version = None
              alias = None
            }
          ]
    }

    let externals = Build.GetExternals(config)
    Assert.Contains(externals, (fun p -> p = "lit"))
    Assert.Contains(externals, (fun p -> p = "lit-v1"))
    Assert.Contains(externals, (fun p -> p = "my-local-dep"))
    Assert.Contains(externals, (fun p -> p = "external-dep"))
