namespace Perla.Tests

open System.Collections.Generic

open Perla.Configuration.Types
open Xunit
open FSharp.UMX

open Perla
open Perla.Types
open Perla.Json.ConfigDecoders
open Perla.Configuration

open FsToolkit.ErrorHandling


module Configuration =
  module FromDecoders =
    open ConfigExtraction

    [<Fact>]
    let ``FromDecoders.GetFable returns None if DecodedFableConfig is not present``
      ()
      =
      let result = FromDecoders.GetFable(None, None)
      Assert.True(result.IsNone)

    [<Fact>]
    let ``FromDecoders.GetFable returns Some if DecodedFableConfig is present``
      ()
      =
      let decoded: DecodedFableConfig = {
        extension = None
        project = None
        outDir = None
        sourceMaps = None
      }

      let result = FromDecoders.GetFable(None, Some decoded)
      Assert.True(result.IsSome)

      match result with
      | Some {
               extension = extension
               project = project
               outDir = outDir
               sourceMaps = sourceMaps
             } ->
        Assert.Equal(
          UMX.untag Defaults.FableConfig.extension,
          UMX.untag extension
        )

        Assert.Equal(UMX.untag Defaults.FableConfig.project, UMX.untag project)
        Assert.True(outDir.IsNone)
        Assert.Equal(Defaults.FableConfig.sourceMaps, sourceMaps)
      | None -> Assert.Fail "Should not be None"

    [<Fact>]
    let ``FromDecoders.GetFable returns respects existing config`` () =
      let expectedProject = "./fsharp/App.fsproj"
      let expectedOutDir = "./src/js"
      let expectedExtension = ".js"

      let defaults = {
        Defaults.FableConfig with
            project = UMX.tag expectedProject
            outDir = Some(UMX.tag expectedOutDir)
      }

      let decoded: DecodedFableConfig = {
        extension = Some(UMX.tag expectedExtension)
        project = None
        outDir = None
        sourceMaps = None
      }

      let result = FromDecoders.GetFable(Some defaults, Some decoded)
      Assert.True(result.IsSome)

      match result with
      | Some {
               extension = extension
               project = project
               outDir = outDir
               sourceMaps = sourceMaps
             } ->
        Assert.Equal(expectedExtension, UMX.untag extension)

        Assert.Equal(expectedProject, UMX.untag project)

        match outDir with
        | Some outDir -> Assert.Equal(expectedOutDir, UMX.untag outDir)
        | None -> Assert.Fail "outDir should be present"

        Assert.Equal(Defaults.FableConfig.sourceMaps, sourceMaps)
      | None -> Assert.Fail "Fable Config should be present"

    [<Fact>]
    let ``FromDecoders.GetDevServer returns None if no DecodedDevServer is present``
      ()
      =
      let config = Defaults.DevServerConfig
      let result = FromDecoders.GetDevServer(config, None)
      Assert.True(result.IsNone)

    [<Fact>]
    let ``FromDecoders.GetDevServer returns DecodedDevServer`` () =
      let config = Defaults.DevServerConfig

      let decoded: DecodedDevServer = {
        port = None
        host = None
        liveReload = None
        useSSL = None
        proxy = None
      }

      let result = FromDecoders.GetDevServer(config, Some decoded)

      match result with
      | Some {
               port = port
               host = host
               liveReload = liveReload
               useSSL = useSSL
               proxy = proxy
             } ->
        Assert.Equal(config.port, port)
        Assert.Equal(config.host, host)
        Assert.Equal(config.liveReload, liveReload)
        Assert.Equal(config.useSSL, useSSL)
        Assert.True(Map.isEmpty proxy)
      | None -> Assert.Fail "DevServer config should be present"

    [<Fact>]
    let ``FromDecoders.GetDevServer respects existing config`` () =
      let expectedPort = 4000
      let expectedHost = "0.0.0.0"
      let expectedUseSSL = false

      let config = {
        Defaults.DevServerConfig with
            port = 8080
            useSSL = expectedUseSSL
      }

      let decoded: DecodedDevServer = {
        port = Some expectedPort
        host = Some expectedHost
        liveReload = None
        useSSL = None
        proxy = None
      }

      let result = FromDecoders.GetDevServer(config, Some decoded)

      match result with
      | Some {
               port = port
               host = host
               liveReload = liveReload
               useSSL = useSSL
               proxy = proxy
             } ->
        Assert.Equal(expectedPort, port)
        Assert.Equal(expectedHost, host)
        Assert.Equal(expectedUseSSL, useSSL)
        Assert.Equal(config.liveReload, liveReload)
        Assert.True(Map.isEmpty proxy)
      | None -> Assert.Fail "DevServer config should be present"

    [<Fact>]
    let ``FromDecoders.GetBuild returns None if no DecodedBuild is present``
      ()
      =
      let config = Defaults.BuildConfig
      let result = FromDecoders.GetBuild(config, None)
      Assert.True(result.IsNone)


    [<Fact>]
    let ``FromDecoders.GetBuild returns DecodedBuild`` () =
      let config = Defaults.BuildConfig

      let decoded: DecodedBuild = {
        excludes = None
        includes = None
        outDir = None
        emitEnvFile = None
      }

      let result = FromDecoders.GetBuild(config, Some decoded)

      match result with
      | Some {
               excludes = excludes
               includes = includes
               outDir = outDir
               emitEnvFile = emitEnvFile
             } ->
        Assert.Empty(includes)

        for expectedExclude in config.excludes do
          Assert.Contains(expectedExclude, excludes)

        Assert.Equal(UMX.untag config.outDir, UMX.untag outDir)

        Assert.Equal(config.emitEnvFile, emitEnvFile)
      | None -> Assert.Fail "Build config should be present"

    [<Fact>]
    let ``FromDecoders.GetBuild respects existing values`` () =

      let expectedOutDir = "./bin/dist"
      let expectedEmitEnvFile = false
      let expectedInclude = "vfs:**/*.html"

      let config = {
        Defaults.BuildConfig with
            includes = [ expectedInclude ]
            emitEnvFile = expectedEmitEnvFile
      }

      let decoded: DecodedBuild = {
        excludes = None
        includes = None
        outDir = Some(UMX.tag expectedOutDir)
        emitEnvFile = None
      }

      let result = FromDecoders.GetBuild(config, Some decoded)

      match result with
      | Some {
               excludes = excludes
               includes = includes
               outDir = outDir
               emitEnvFile = emitEnvFile
             } ->
        Assert.Contains(expectedInclude, includes)

        for expectedExclude in config.excludes do
          Assert.Contains(expectedExclude, excludes)

        Assert.Equal(expectedOutDir, UMX.untag outDir)
        Assert.Equal(expectedEmitEnvFile, emitEnvFile)
      | None -> Assert.Fail "DevServer config should be present"


    [<Fact>]
    let ``FromDecoders.GetEsbuild returns None if no DecodedBuild is present``
      ()
      =
      let config = Defaults.EsbuildConfig
      let result = FromDecoders.GetEsbuild(config, None)
      Assert.True(result.IsNone)


    [<Fact>]
    let ``FromDecoders.GetEsbuild returns DecodedBuild`` () =
      let config = Defaults.EsbuildConfig

      let decoded: DecodedEsbuild = {
        externals = None
        injects = None
        minify = None
        version = None
        ecmaVersion = None
        fileLoaders = None
        jsxAutomatic = None
        esBuildPath = None
        jsxImportSource = None
      }

      let result = FromDecoders.GetEsbuild(config, Some decoded)

      match result with
      | Some {
               externals = externals
               injects = injects
               minify = minify
               version = version
               ecmaVersion = ecmaVersion
               fileLoaders = fileLoaders
               jsxAutomatic = jsxAutomatic
               esBuildPath = esBuildPath
               jsxImportSource = jsxImportSource
             } ->
        Assert.Empty(externals)
        Assert.Empty(injects)
        Assert.Equal(config.minify, minify)
        Assert.Equal(config.ecmaVersion, ecmaVersion)
        Assert.Equal(UMX.untag config.esBuildPath, UMX.untag esBuildPath)
        Assert.Equal(UMX.untag config.version, UMX.untag version)
        Assert.Equal(config.jsxImportSource, jsxImportSource)
        Assert.Equal(config.jsxAutomatic, jsxAutomatic)
        let loaders = fileLoaders :> IDictionary<string, string>

        for KeyValue(expectedKey, expectedValue) in config.fileLoaders do
          let value = Assert.Contains(expectedKey, loaders)
          Assert.Equal(expectedValue, value)

      | None -> Assert.Fail "Build config should be present"

    [<Fact>]
    let ``FromDecoders.GetEsbuild respects existing values`` () =
      let expectedVersion = "0.0.0"
      let expectedEcmaVersion = "ES2022"
      let expectedMinify = false
      let expectedEsbuildPath = "/path/to/esbuild"

      let config = {
        Defaults.EsbuildConfig with
            version = UMX.tag expectedVersion
            ecmaVersion = expectedEcmaVersion
      }

      let decoded: DecodedEsbuild = {
        externals = None
        injects = None
        minify = Some expectedMinify
        version = None
        ecmaVersion = None
        fileLoaders = None
        jsxAutomatic = None
        esBuildPath = Some(UMX.tag expectedEsbuildPath)
        jsxImportSource = None
      }

      let result = FromDecoders.GetEsbuild(config, Some decoded)

      match result with
      | Some {
               externals = externals
               injects = injects
               minify = minify
               version = version
               ecmaVersion = ecmaVersion
               fileLoaders = fileLoaders
               jsxAutomatic = jsxAutomatic
               esBuildPath = esBuildPath
               jsxImportSource = jsxImportSource
             } ->
        Assert.Empty(externals)
        Assert.Empty(injects)
        Assert.Equal(expectedMinify, minify)
        Assert.Equal(expectedVersion, version)
        Assert.Equal(expectedEcmaVersion, ecmaVersion)
        Assert.Equal(expectedEsbuildPath, UMX.untag esBuildPath)
        Assert.Equal(UMX.untag config.version, UMX.untag version)
        Assert.Equal(config.jsxImportSource, jsxImportSource)
        Assert.Equal(config.jsxAutomatic, jsxAutomatic)
        let loaders = fileLoaders :> IDictionary<string, string>

        for KeyValue(expectedKey, expectedValue) in config.fileLoaders do
          let value = Assert.Contains(expectedKey, loaders)
          Assert.Equal(expectedValue, value)

      | None -> Assert.Fail "Build config should be present"


    [<Fact>]
    let ``FromDecoders.GetTesting returns None if no DecodedBuild is present``
      ()
      =
      let config = Defaults.TestConfig
      let result = FromDecoders.GetTesting(config, None)
      Assert.True(result.IsNone)


    [<Fact>]
    let ``FromDecoders.GetTesting returns DecodedBuild`` () =
      let config = Defaults.TestConfig

      let decoded: DecodedTesting = {
        includes = None
        excludes = None
        browserMode = None
        browsers = None
        headless = None
        watch = None
        fable = None
      }

      let result = FromDecoders.GetTesting(config, Some decoded)

      match result with
      | Some {
               includes = includes
               excludes = excludes
               browserMode = browserMode
               browsers = browsers
               headless = headless
               watch = watch
             } ->

        Assert.Contains(Browser.Chromium, browsers)
        Assert.Empty(excludes)

        for expectedInclude in config.includes do
          Assert.Contains(expectedInclude, includes)

        Assert.Equal(config.watch, watch)
        Assert.Equal(config.headless, headless)
        Assert.Equal(config.browserMode, browserMode)
        Assert.True(config.fable.IsNone)
      | None -> Assert.Fail "Build config should be present"

    [<Fact>]
    let ``FromDecoders.GetTesting respects existing values`` () =
      let expectedHeadless = true
      let expectedBrowser = Browser.Firefox
      let expectedBrowserMode = BrowserMode.Sequential
      let expectedExclude = "**/*.no-test.js"

      let config = {
        Defaults.TestConfig with
            headless = expectedHeadless
            browsers = [ expectedBrowser ]
            browserMode = BrowserMode.Parallel
      }

      let decoded: DecodedTesting = {
        includes = None
        excludes = Some [ expectedExclude ]
        browserMode = Some expectedBrowserMode
        browsers = None
        headless = None
        watch = None
        fable = None
      }

      let result = FromDecoders.GetTesting(config, Some decoded)

      match result with
      | Some {
               includes = includes
               excludes = excludes
               browserMode = browserMode
               browsers = browsers
               headless = headless
               watch = watch
             } ->
        (expectedBrowser, Assert.Single(browsers)) |> Assert.Equal
        (expectedExclude, Assert.Single(excludes)) |> Assert.Equal

        for expectedInclude in config.includes do
          Assert.Contains(expectedInclude, includes)

        Assert.Equal(config.watch, watch)
        Assert.Equal(expectedHeadless, headless)
        Assert.Equal(expectedBrowserMode, browserMode)
        Assert.True(config.fable.IsNone)
      | None -> Assert.Fail "Build config should be present"

  module FromFields =
    open ConfigExtraction

    [<Fact>]
    let ``FromFields.GetServerFields gives you the default values`` () =
      let config = Defaults.DevServerConfig
      let serverOptions = FromFields.GetServerFields(config, None)

      let expectedOptions = seq {
        DevServerField.Port config.port
        DevServerField.Host config.host
        DevServerField.LiveReload config.liveReload
        DevServerField.UseSSL config.useSSL
      }

      Assert.Equal<DevServerField>(expectedOptions, serverOptions)

    [<Fact>]
    let ``FromFields.GetServerFields gives you provided values`` () =
      let config = Defaults.DevServerConfig

      let expectedOptions = seq {
        DevServerField.Port 1000
        DevServerField.Host "0.0.0.0"
      }

      let serverOptions =
        FromFields.GetServerFields(config, Some expectedOptions)

      for expectedOption in expectedOptions do
        Assert.Contains(expectedOption, serverOptions)


    [<Fact>]
    let ``FromFields.GetMinify Returns minification based on run config`` () =
      let notMinified =
        FromFields.GetMinify(RunConfiguration.Development, Seq.empty)

      let minified =
        FromFields.GetMinify(RunConfiguration.Production, Seq.empty)

      Assert.False(notMinified)
      Assert.True(minified)

    [<Fact>]
    let ``FromFields.GetMinify can extract MinifySources from field seq`` () =
      let minified =
        FromFields.GetMinify(
          RunConfiguration.Development,
          seq {
            MinifySources true
            DevServerField.Port 1000
          }
        )

      let notMinified =
        FromFields.GetMinify(
          RunConfiguration.Production,
          seq {
            MinifySources false
            DevServerField.Port 1000
          }
        )

      Assert.True(minified)
      Assert.False(notMinified)

    [<Fact>]
    let ``FromFields.GetDevServerOptions returns the same config passed if the fields are empty``
      ()
      =
      let options =
        FromFields.GetDevServerOptions(Defaults.DevServerConfig, Seq.empty)

      Assert.Equal(Defaults.DevServerConfig, options)

    [<Fact>]
    let ``FromFields.GetDevServerOptions can extract DevServer Options from field seq``
      ()
      =

      let expectedOptions = {
        Defaults.DevServerConfig with
            port = 1000
            host = "0.0.0.0"
      }

      let options =
        FromFields.GetDevServerOptions(
          Defaults.DevServerConfig,
          seq {
            DevServerField.Host "0.0.0.0"
            DevServerField.Port 1000
          }
        )

      Assert.Equal(expectedOptions, options)

    [<Fact>]
    let ``FromFields.GetTesting returns the same config passed if the fields are empty``
      ()
      =
      let options = FromFields.GetTesting(Defaults.TestConfig, None)
      Assert.Equal<Browser>(Defaults.TestConfig.browsers, options.browsers)
      Assert.Equal<string>(Defaults.TestConfig.includes, options.includes)
      Assert.Equal<string>(Defaults.TestConfig.excludes, options.excludes)
      Assert.Equal(Defaults.TestConfig.watch, options.watch)
      Assert.Equal(Defaults.TestConfig.headless, options.headless)
      Assert.Equal(Defaults.TestConfig.browserMode, options.browserMode)
      Assert.Equal(Defaults.TestConfig.fable, options.fable)

    [<Fact>]
    let ``FromFields.GetTesting can extract DevServer Options from field seq``
      ()
      =

      let expectedOptions = {
        Defaults.TestConfig with
            browsers = [ Browser.Firefox ]
            headless = false
      }

      let options =
        FromFields.GetTesting(
          Defaults.TestConfig,
          Some(
            seq {
              TestingField.Browsers [ Browser.Firefox ]
              TestingField.Headless false
            }
          )
        )

      Assert.Equal<Browser>(expectedOptions.browsers, options.browsers)
      Assert.Equal<string>(expectedOptions.includes, options.includes)
      Assert.Equal<string>(expectedOptions.excludes, options.excludes)
      Assert.Equal(expectedOptions.watch, options.watch)
      Assert.Equal(expectedOptions.headless, options.headless)
      Assert.Equal(expectedOptions.browserMode, options.browserMode)
      Assert.Equal(expectedOptions.fable, options.fable)

  // we're not picking up env variables yet for perla config
  [<Fact>]
  let ``fromEnv returns the same config passed`` () =
    let config = Defaults.PerlaConfig
    Assert.Equal(config, ConfigExtraction.FromEnv config)

  [<Fact>]
  let ``fromFile should update devServer options in perla config`` () =
    let config = Defaults.PerlaConfig

    let configText =
      """{
  "devServer": {
    "port": 3000,
    "host": "0.0.0.0",
    // skip "liveReload" just to ensure the defaults are respected
    "useSSL": false,
    "proxy": { "/reqUrl": "https://target" }
  }
}"""

    let parsedConfig = Json.getConfigDocument configText
    let result = ConfigExtraction.FromFile (Some parsedConfig) config

    Assert.Equal(
      result,
      {
        config with
            devServer = {
              port = 3000
              host = "0.0.0.0"
              liveReload = true
              useSSL = false
              proxy = Map.ofList [ "/reqUrl", "https://target" ]
            }
      }
    )
