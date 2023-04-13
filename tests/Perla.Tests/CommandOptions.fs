namespace Perla.Tests

open Xunit
open System.CommandLine
open System.CommandLine.Parsing

open FSharp.SystemCommandLine.Inputs

open FsToolkit.ErrorHandling

open Perla
open Perla.Types
open Perla.Commands
open Perla.PackageManager.Types

[<AutoOpen>]
module Extensions =

  type HandlerInput<'a> with

    member this.GetValue(ctx: ParseResult) : 'T =
      match this.Source with
      | ParsedOption o -> o :?> Option<'T> |> ctx.GetValueForOption
      | ParsedArgument a -> a :?> Argument<'T> |> ctx.GetValueForArgument
      | Context -> failwith "Unable to get a result from context"


module CommandOptions =

  let ParseRootCommand (handler: Command, command: string) : ParseResult =
    let root = RootCommand()
    root.AddCommand(handler)
    root.Parse(command)

  let ParseInput (input: HandlerInput<'T>, token: string) : ParseResult =
    match input.Source with
    | ParsedOption o ->
      let option = o :?> Option<'T>
      option.Parse(token)
    | ParsedArgument a ->
      let argument = a :?> Argument<'T>
      argument.Parse(token)
    | Context -> failwith "Unable to "

  module Inputs =
    open Perla.Handlers

    [<Fact>]
    let ``SharedInputs.source can parse it's sources`` () =
      let jspm = ParseInput(SharedInputs.source, "-s jspm")
      let skypack = ParseInput(SharedInputs.source, "--source skypack")
      let unpkg = ParseInput(SharedInputs.source, "--source unpkg")
      let jsdelivr = ParseInput(SharedInputs.source, "-s jsdelivr")
      let esmsh = ParseInput(SharedInputs.source, "-s esm.sh")
      let jspmSystem = ParseInput(SharedInputs.source, "-s jspm.system")
      let jspmSystem1 = ParseInput(SharedInputs.source, "--source jspm#system")

      let jspm: Provider voption = SharedInputs.source.GetValue jspm
      let skypack: Provider voption = SharedInputs.source.GetValue skypack
      let unpkg: Provider voption = SharedInputs.source.GetValue unpkg
      let jsdelivr: Provider voption = SharedInputs.source.GetValue jsdelivr
      let esmsh: Provider voption = SharedInputs.source.GetValue esmsh
      let jspmSystem: Provider voption = SharedInputs.source.GetValue jspmSystem

      let jspmSystem1: Provider voption =
        SharedInputs.source.GetValue jspmSystem1


      Assert.Equal(Provider.Jspm, jspm.Value)
      Assert.Equal(Provider.Skypack, skypack.Value)
      Assert.Equal(Provider.Unpkg, unpkg.Value)
      Assert.Equal(Provider.Jsdelivr, jsdelivr.Value)
      Assert.Equal(Provider.EsmSh, esmsh.Value)
      Assert.Equal(Provider.JspmSystem, jspmSystem.Value)
      Assert.Equal(Provider.JspmSystem, jspmSystem1.Value)

    [<Fact>]
    let ``SharedInputs.mode can parse it's sources`` () =
      let dev = ParseInput(SharedInputs.mode, "-m dev")
      let dev1 = ParseInput(SharedInputs.mode, "--mode development")
      let prod = ParseInput(SharedInputs.mode, "-m prod")
      let prod1 = ParseInput(SharedInputs.mode, "--mode production")

      let dev: RunConfiguration voption = SharedInputs.mode.GetValue dev
      let dev1: RunConfiguration voption = SharedInputs.mode.GetValue dev1

      let prod: RunConfiguration voption = SharedInputs.mode.GetValue prod
      let prod1: RunConfiguration voption = SharedInputs.mode.GetValue prod1

      Assert.Equal(RunConfiguration.Development, dev.Value)
      Assert.Equal(RunConfiguration.Development, dev1.Value)
      Assert.Equal(RunConfiguration.Production, prod.Value)
      Assert.Equal(RunConfiguration.Production, prod1.Value)

    [<Fact>]
    let ``TestingInputs.browsers can parse it's browsers`` () =
      let chromium = ParseInput(TestingInputs.browsers, "-b chromium")
      let firefox = ParseInput(TestingInputs.browsers, "--browsers firefox")
      let webkit = ParseInput(TestingInputs.browsers, "-b webkit")
      let edge = ParseInput(TestingInputs.browsers, "--browsers edge")
      let chrome = ParseInput(TestingInputs.browsers, "-b chrome")

      let multiple =
        ParseInput(
          TestingInputs.browsers,
          "-b firefox edge -b chrome -b webkit chromium"
        )

      let chromium: Browser array = TestingInputs.browsers.GetValue chromium
      let firefox: Browser array = TestingInputs.browsers.GetValue firefox
      let webkit: Browser array = TestingInputs.browsers.GetValue webkit
      let edge: Browser array = TestingInputs.browsers.GetValue edge
      let chrome: Browser array = TestingInputs.browsers.GetValue chrome
      let multiple: Browser array = TestingInputs.browsers.GetValue multiple

      (Browser.Chromium, Assert.Single chromium) |> Assert.Equal
      (Browser.Firefox, Assert.Single firefox) |> Assert.Equal
      (Browser.Webkit, Assert.Single webkit) |> Assert.Equal
      (Browser.Edge, Assert.Single edge) |> Assert.Equal
      (Browser.Chrome, Assert.Single chrome) |> Assert.Equal

      match multiple with
      | [| Browser.Firefox
           Browser.Edge
           Browser.Chrome
           Browser.Webkit
           Browser.Chromium |] -> ()
      | others ->
        Assert.Fail(
          $"Expected 5 browsers, but got %i{others.Length} browsers: %A{others}"
        )

    [<Fact>]
    let ``TemplateInputs.displayMode`` () =
      let table = ParseInput(TemplateInputs.displayMode, "--list table")
      let text = ParseInput(TemplateInputs.displayMode, "-ls text")
      let other = ParseInput(TemplateInputs.displayMode, "-ls other")

      let table: ListFormat = TemplateInputs.displayMode.GetValue table
      let text: ListFormat = TemplateInputs.displayMode.GetValue text

      let argError = Assert.Single other.Errors

      Assert.Equal(
        """Argument 'other' not recognized. Must be one of:
	'table'
	'text'""",
        argError.Message
      )

      Assert.Equal(ListFormat.HumanReadable, table)
      Assert.Equal(ListFormat.TextOnly, text)

  [<Fact>]
  let ``Commands.Setup can parse options`` () =
    let result =
      ParseRootCommand(Commands.Setup, "setup -y --skip-playwright false")

    let skipPlaywright: bool option = SetupInputs.skipPlaywright.GetValue result

    let skipPrompts: bool option = SetupInputs.skipPrompts.GetValue result

    Assert.Empty(result.Errors)
    Assert.True(skipPrompts |> Option.defaultValue false)
    Assert.False(skipPlaywright |> Option.defaultValue true)

  [<Fact>]
  let ``Parse Commands.Setup without options should not fail`` () =
    let result = ParseRootCommand(Commands.Setup, "setup")

    let skipPlaywright: bool option = SetupInputs.skipPlaywright.GetValue result

    let skipPrompts: bool option = SetupInputs.skipPrompts.GetValue result

    Assert.Empty(result.Errors)
    Assert.True(skipPrompts |> Option.isNone)
    Assert.True(skipPlaywright |> Option.isNone)

  [<Fact>]
  let ``Commands.Build can parse options`` () =
    let result =
      ParseRootCommand(
        Commands.Build,
        "build --dev -epl false -rim --preview false"
      )

    let asDev: bool option = SharedInputs.asDev.GetValue result

    let enablePreloads: bool option = BuildInputs.enablePreloads.GetValue result

    let rebuildImportMap: bool option =
      BuildInputs.rebuildImportMap.GetValue result

    let preview: bool option = BuildInputs.preview.GetValue result


    Assert.Empty(result.Errors)
    Assert.True(asDev.Value)
    Assert.False(enablePreloads.Value)
    Assert.True(rebuildImportMap.Value)
    Assert.False(preview.Value)

  [<Fact>]
  let ``Commands.Build doesn't need all of the options`` () =
    let result = ParseRootCommand(Commands.Build, "build")


    let asDev: bool option = SharedInputs.asDev.GetValue result

    let enablePreloads: bool option = BuildInputs.enablePreloads.GetValue result

    let rebuildImportMap: bool option =
      BuildInputs.rebuildImportMap.GetValue result

    let preview: bool option = BuildInputs.preview.GetValue result


    Assert.Empty(result.Errors)
    Assert.True(asDev.IsNone)
    Assert.True(enablePreloads.IsNone)
    Assert.True(rebuildImportMap.IsNone)
    Assert.True(preview.IsNone)

  [<Fact>]
  let ``Commands.Serve can parse options`` () =
    let expectedPort = 3400
    let expectedHost = "0.0.0.0"

    let result =
      ParseRootCommand(
        Commands.Serve,
        $"serve -d --port %i{expectedPort} --host %s{expectedHost} --ssl false"
      )

    let asDev: bool option = SharedInputs.asDev.GetValue result
    let port: int option = ServeInputs.port.GetValue result
    let host: string option = ServeInputs.host.GetValue result
    let ssl: bool option = ServeInputs.ssl.GetValue result

    Assert.Empty(result.Errors)
    Assert.True(asDev.Value)
    Assert.Equal(expectedPort, port.Value)
    Assert.Equal(expectedHost, host.Value)
    Assert.False(ssl.Value)

  [<Fact>]
  let ``Commands.Serve requires no options`` () =
    let result = ParseRootCommand(Commands.Serve, "serve")

    let asDev: bool option = SharedInputs.asDev.GetValue result
    let port: int option = ServeInputs.port.GetValue result
    let host: string option = ServeInputs.host.GetValue result
    let ssl: bool option = ServeInputs.ssl.GetValue result

    Assert.Empty(result.Errors)
    Assert.True(asDev.IsNone)
    Assert.True(port.IsNone)
    Assert.True(host.IsNone)
    Assert.True(ssl.IsNone)

  [<Fact>]
  let ``Commands.SearchPackage requires package name`` () =
    let result = ParseRootCommand(Commands.SearchPackage, "search")

    let error = Assert.Single(result.Errors)

    Assert.Equal(
      "Required argument missing for command: 'search'.",
      error.Message
    )

  [<Fact>]
  let ``Commands.SearchPackage can parse the page value`` () =
    let expectedPage = 2
    let expectedPackage = "lodash"

    let result =
      ParseRootCommand(
        Commands.SearchPackage,
        $"search %s{expectedPackage} -p %i{expectedPage}"
      )

    let package: string = PackageInputs.package.GetValue result
    let page: int option = PackageInputs.currentPage.GetValue result

    Assert.Empty result.Errors
    Assert.Equal(expectedPackage, package)
    Assert.Equal(expectedPage, page.Value)

  [<Fact>]
  let ``Commands.ShowPackage requires package name`` () =
    let result = ParseRootCommand(Commands.ShowPackage, "show")

    let error = Assert.Single(result.Errors)

    Assert.Equal(
      "Required argument missing for command: 'show'.",
      error.Message
    )

  [<Fact>]
  let ``Commands.RemovePackage can parse an alias`` () =
    let expectedAlias = "lodash-3"
    let expectedPackage = "lodash@3"

    let result =
      ParseRootCommand(
        Commands.RemovePackage,
        $"remove %s{expectedPackage} -a %s{expectedAlias}"
      )

    let package: string = PackageInputs.package.GetValue result
    let alias: string option = PackageInputs.alias.GetValue result

    Assert.Empty result.Errors
    Assert.Equal(expectedPackage, package)
    Assert.Equal(expectedAlias, alias.Value)

  [<Fact>]
  let ``Commands.AddPackage can parse options`` () =
    let expectedSource = Provider.Unpkg
    let expectedPackage = "lodash"
    let expectedAlias = "lodash-3"
    let expectedVersion = "3.0.0"

    let result =
      ParseRootCommand(
        Commands.AddPackage,
        $"add %s{expectedPackage} -d -s %s{expectedSource.AsString} --version %s{expectedVersion} -a %s{expectedAlias}"
      )

    let asDev: bool option = SharedInputs.asDev.GetValue result
    let source: Provider voption = SharedInputs.source.GetValue result
    let package: string = PackageInputs.package.GetValue result
    let alias: string option = PackageInputs.alias.GetValue result
    let version: string option = PackageInputs.version.GetValue result

    Assert.Empty result.Errors
    Assert.True(asDev.Value)
    Assert.Equal(expectedSource, source.Value)
    Assert.Equal(expectedPackage, package)
    Assert.Equal(expectedAlias, alias.Value)
    Assert.Equal(expectedVersion, version.Value)
