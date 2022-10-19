namespace Perla.Tests

open System

open Xunit

open FSharp.UMX

open Perla
open Perla.Units
open Perla.Configuration
open Perla.Esbuild

type Esbuild() =

  do
    let result =
      Perla.FileSystem.FileSystem.SetupEsbuild(
        UMX.tag Constants.Esbuild_Version
      )

    result.GetAwaiter().GetResult()

  [<Fact>]
  member _.``GetPlugin should provide an esbuild plugin``() =
    let plugin = Esbuild.GetPlugin(Defaults.EsbuildConfig)

    Assert.False(plugin.shouldProcessFile.IsNone)
    Assert.False(plugin.transform.IsNone)

    Assert.True(plugin.shouldProcessFile.Value ".jsx")
    Assert.True(plugin.shouldProcessFile.Value ".tsx")
    Assert.True(plugin.shouldProcessFile.Value ".ts")
    Assert.True(plugin.shouldProcessFile.Value ".js")
    Assert.True(plugin.shouldProcessFile.Value ".css")

  [<Fact>]
  member _.``Esbuild Plugin should process JSX``() =
    task {
      let plugin =
        Esbuild.GetPlugin(
          { Defaults.EsbuildConfig with
              minify = false
              jsxFactory = Some "h"
              jsxFragment = Some "Fragment" }
        )

      match plugin.transform with
      | ValueSome transform ->
        let! result =
          transform
            { content = "const a = <a>hello</a>"
              extension = ".jsx" }

        Assert.Equal(".js", result.extension)

        Assert.Equal(
          """const a = /* @__PURE__ */ h("a", null, "hello");
""",
          result.content
        )
      | ValueNone -> Assert.Fail "Esbuild plugin must have a transform function"
    }

  [<Fact>]
  member _.``Esbuild Plugin should process TSX``() =
    task {
      let plugin =
        Esbuild.GetPlugin(
          { Defaults.EsbuildConfig with
              minify = false
              jsxFactory = Some "h"
              jsxFragment = Some "Fragment" }
        )

      match plugin.transform with
      | ValueSome transform ->
        let! result =
          transform
            { content = "const b: string = \"hello\";\nconst a = <a>{b}</a>"
              extension = ".tsx" }

        Assert.Equal(".js", result.extension)

        Assert.Equal(
          """const b = "hello";
const a = /* @__PURE__ */ h("a", null, b);
""",
          result.content
        )
      | ValueNone -> Assert.Fail "Esbuild plugin must have a transform function"
    }

  [<Fact>]
  member _.``Esbuild Plugin should process Typescript``() =
    task {
      let plugin =
        Esbuild.GetPlugin({ Defaults.EsbuildConfig with minify = false })

      match plugin.transform with
      | ValueSome transform ->
        let! result =
          transform
            { content =
                "const b: string = \"hello\";\nconst a = (msg: string) => console.log(msg);\na(b);"
              extension = ".ts" }

        Assert.Equal(".js", result.extension)

        Assert.Equal(
          """const b = "hello";
const a = (msg) => console.log(msg);
a(b);
""",
          result.content
        )
      | ValueNone -> Assert.Fail "Esbuild plugin must have a transform function"
    }

  [<Fact>]
  member _.``Esbuild Plugin should process Javascript``() =
    task {
      let plugin =
        Esbuild.GetPlugin(
          { Defaults.EsbuildConfig with
              minify = false
              ecmaVersion = "es2016" }
        )

      match plugin.transform with
      | ValueSome transform ->
        let! result =
          transform
            { content =
                "const a = msg => console.log(msg ?? 'no message');\na(msg);"
              extension = ".js" }

        Assert.Equal(".js", result.extension)

        Assert.Equal(
          """const a = (msg2) => console.log(msg2 != null ? msg2 : "no message");
a(msg);
""",
          result.content
        )
      | ValueNone -> Assert.Fail "Esbuild plugin must have a transform function"
    }

  [<Fact>]
  member _.``Esbuild Plugin should process CSS``() =
    task {
      let plugin =
        Esbuild.GetPlugin(
          { Defaults.EsbuildConfig with
              ecmaVersion = "esnext,chrome100"
              minify = true }
        )

      match plugin.transform with
      | ValueSome transform ->
        let! result =
          transform
            { content = "body { color: rgba(255, 0, 0, 0.4) }"
              extension = ".css" }

        Assert.Equal(".css", result.extension)

        Assert.Equal(
          """body{color:#f006}
""",
          result.content
        )
      | ValueNone -> Assert.Fail "Esbuild plugin must have a transform function"
    }
