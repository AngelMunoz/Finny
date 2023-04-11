[<RequireQualifiedAccess>]
module Perla.Commands

open System.Threading

open System.CommandLine
open System.CommandLine.Invocation
open System.CommandLine.Parsing

open FSharp.SystemCommandLine
open FSharp.SystemCommandLine.Aliases

open FsToolkit.ErrorHandling

open Perla
open Perla.PackageManager.Types
open Perla.Types
open Perla.Handlers

type Input with

  static member OptionWithStrings
    (
      aliases: string seq,
      ?values: string seq,
      ?defaultValue: string,
      ?description
    ) =
    let option =
      Opt<string option>(
        aliases |> Array.ofSeq,
        getDefaultValue = (fun _ -> defaultValue),
        ?description = description
      )

    match values with
    | Some values -> option.FromAmong(values |> Array.ofSeq)
    | None -> option
    |> HandlerInput.OfOption

  static member MultipleStrings
    (
      name: string,
      ?values: string seq,
      ?description
    ) =
    let option =
      Option<string[] option>(
        name = name,
        ?description = description,
        IsRequired = false,
        AllowMultipleArgumentsPerToken = true,
        parseArgument =
          (fun result ->
            match result.Tokens |> Seq.toArray with
            | [||] -> None
            | others -> Some(others |> Array.map (fun token -> token.Value)))
      )

    match values with
    | Some values -> option.FromAmong(values |> Array.ofSeq)
    | None -> option
    |> HandlerInput.OfOption

  static member ArgumentWithStrings
    (
      name: string,
      ?values: string seq,
      ?defaultValue: string,
      ?description
    ) =
    let arg =
      Arg<string option>(
        name,
        getDefaultValue = (fun _ -> defaultValue),
        ?description = description
      )

    match values with
    | Some values -> arg.FromAmong(values |> Array.ofSeq)
    | None -> arg
    |> HandlerInput.OfArgument

  static member ArgumentMaybe(name: string, ?values: string seq, ?description) =
    let arg =
      Arg<string option>(
        name,
        parse =
          (fun argResult ->
            match argResult.Tokens |> Seq.toList with
            | [] -> None
            | [ token ] -> Some token.Value
            | _ :: _ ->
              failwith "F# Option can only be used with a single argument."),
        ?description = description
      )

    match values with
    | Some values -> arg.FromAmong(values |> Array.ofSeq)
    | None -> arg
    |> HandlerInput.OfArgument


let runAsDev =
  Input.OptionMaybe(
    [ "--development"; "-d"; "--dev" ],
    "Use Dev dependencies when running, defaults to false"
  )

let Build =
  let enablePreloads =
    Input.OptionMaybe(
      [ "-epl"; "--enable-preload-links" ],
      "enable adding modulepreload links in the final build"
    )

  let rebuildImportMap =
    Input.OptionMaybe(
      [ "-rim"; "--rebuild-importmap" ],
      "discards the current import map (and custom resolutions)
       and generates a new one based on the dependencies listed in the config file."
    )

  let preview =
    Input.OptionMaybe(
      [ "-prev"; "--preview" ],
      "discards the current import map (and custom resolutions)
       and generates a new one based on the dependencies listed in the config file."
    )

  let buildArgs
    (
      context: InvocationContext,
      runAsDev: bool option,
      enablePreloads: bool option,
      rebuildImportMap: bool option,
      enablePreview: bool option
    ) =
    { mode =
        runAsDev
        |> Option.map (fun runAsDev ->
          match runAsDev with
          | true -> RunConfiguration.Development
          | false -> RunConfiguration.Production)
      enablePreloads = defaultArg enablePreloads true
      rebuildImportMap = defaultArg rebuildImportMap false
      enablePreview = defaultArg enablePreview false },
    context.GetCancellationToken()

  command "build" {
    description "Builds the SPA application for distribution"
    addAlias "b"
    inputs (
      Input.Context(),
      runAsDev,
      enablePreloads,
      rebuildImportMap,
      preview
    )

    setHandler (buildArgs >> Handlers.runBuild)
  }

let Serve =

  let port =
    Input.OptionMaybe<int>(
      [ "--port"; "-p" ],
      "Port where the application starts"
    )

  let host =
    Input.OptionMaybe<string>(
      [ "--host" ],
      "network ip address where the application will run"
    )

  let ssl = Input.OptionMaybe<bool>([ "--ssl" ], "Run dev server with SSL")


  let buildArgs
    (
      context: InvocationContext,
      mode: bool option,
      port: int option,
      host: string option,
      ssl: bool option
    ) =
    { mode =
        mode
        |> Option.map (fun runAsDev ->
          match runAsDev with
          | true -> RunConfiguration.Development
          | false -> RunConfiguration.Production)
      port = port
      host = host
      ssl = ssl },
    context.GetCancellationToken()

  let desc =
    "Starts the development server and if fable projects are present it also takes care of it."

  command "serve" {
    description desc
    addAliases ["s"; "start"]
    inputs (Input.Context(), runAsDev, port, host, ssl)
    setHandler (buildArgs >> Handlers.runServe)
  }

let Setup =
  let skipPrompts =
    Input.OptionMaybe<bool>(
      [ "--skip-prompts"; "-y"; "--yes"; "-sp" ],
      "Skip prompts"
    )

  let playwrightDeps =
    Input.OptionMaybe<bool>(
      [ "--skip-playwright" ],
      "Skips installing playwright (defaults to true)"
    )

  let buildArgs
    (
      ctx: InvocationContext,
      yes: bool option,
      skipPlaywright: bool option
    ) : SetupOptions * CancellationToken =
    { skipPrompts = yes |> Option.defaultValue false
      skipPlaywright = skipPlaywright |> Option.defaultValue true },
    ctx.GetCancellationToken()


  command "setup" {
    description "Initialized a given directory or perla itself"
    inputs (Input.Context(), skipPrompts, playwrightDeps)
    setHandler (buildArgs >> Handlers.runSetup)
  }

let SearchPackage =
  let package =
    Input.OptionRequired(
      "package",
      "The package you want to search for in the Skypack api"
    )

  let page =
    Input.OptionMaybe(
      [| "--page"; "-p" |],
      "change the page number in the search results"
    )

  let buildArgs
    (
      ctx: InvocationContext,
      package: string,
      page: int option
    ) : SearchOptions * CancellationToken =
    { package = package
      page = page |> Option.defaultValue 1 },
    ctx.GetCancellationToken()

  command "search" {
    description
      "Search a package name in the Skypack api, this will bring potential results"

    inputs (Input.Context(), package, page)
    setHandler (buildArgs >> Handlers.runSearchPackage)
  }

let ShowPackage =
  let package =
    Input.Argument(
      "package",
      "The package you want to search for in the Skypack api"
    )

  let buildArgs
    (
      ctx: InvocationContext,
      package: string
    ) : ShowPackageOptions * CancellationToken =
    { package = package }, ctx.GetCancellationToken()

  command "show" {
    description
      "Shows information about a package if the name matches an existing one"

    inputs (Input.Context(), package)
    setHandler (buildArgs >> Handlers.runShowPackage)
  }

let RemovePackage =
  let package =
    Input.Argument(
      "package",
      "The package you want to search for in the Skypack api"
    )

  let alias =
    Input.OptionMaybe(
      [ "--alias"; "-a" ],
      "the alias of the package if you added one"
    )

  let buildArgs
    (
      ctx: InvocationContext,
      package: string,
      alias: string option
    ) : RemovePackageOptions * CancellationToken =
    { package = package; alias = alias }, ctx.GetCancellationToken()

  command "remove" {
    description "removes a package from the "

    inputs (Input.Context(), package, alias)
    setHandler (buildArgs >> Handlers.runRemovePackage)
  }

let AddPackage =
  let package =
    Input.Argument(
      "package",
      "The package you want to search for in the skypack api"
    )

  let version =
    let opt =
      Aliases.Opt<string option>(
        [| "-v"; "--version" |],
        parseArgument =
          (fun arg ->
            arg.Tokens
            |> Seq.tryHead
            |> Option.map (fun token -> token.Value |> Option.ofObj)
            |> Option.flatten)
      )

    opt |> HandlerInput.OfOption

  let source =
    Input.OptionWithStrings(
      [ "--source"; "-s" ],
      [ "jspm"; "skypack"; "unpkg"; "jsdelivr"; "jspm.system" ],
      "jspm",
      "CDN that will be used to fetch dependencies from"
    )

  let dev =
    Input.OptionMaybe(
      [ "--dev"; "--development"; "-d" ],
      "Adds this dependency to the dev dependencies"
    )

  let alias =
    Input.OptionMaybe(
      [ "--alias"; "-a" ],
      "the alias of the package if you added one"
    )

  let buildArgs
    (
      ctx: InvocationContext,
      package: string,
      version: string option,
      source: string option,
      dev: bool option,
      alias: string option
    ) : AddPackageOptions * CancellationToken =
    { package = package
      version = version
      source = source |> Option.map Provider.FromString
      mode =
        dev
        |> Option.map (fun dev ->
          if dev then
            RunConfiguration.Development
          else
            RunConfiguration.Production)
      alias = alias },
    ctx.GetCancellationToken()

  command "add" {
    description
      "Shows information about a package if the name matches an existing one"
    addAlias "install"
    inputs (Input.Context(), package, version, source, dev, alias)
    setHandler (buildArgs >> Handlers.runAddPackage)
  }

let ListPackages =

  let asNpm =
    Input.OptionMaybe(
      [ "--npm"; "--as-package-json"; "-j" ],
      "Show the packages simlar to npm's package.json"
    )

  let buildArgs (asNpm: bool option) : ListPackagesOptions =
    { format =
        asNpm
        |> Option.map (fun asNpm ->
          if asNpm then
            ListFormat.TextOnly
          else
            ListFormat.HumanReadable)
        |> Option.defaultValue ListFormat.HumanReadable }

  command "list" {
    addAlias "ls"
    description
      "Lists the current dependencies in a table or an npm style json string"

    inputs asNpm
    setHandler (buildArgs >> Handlers.runListPackages)
  }

let RestoreImportMap =
  let source =
    Input.OptionWithStrings(
      [ "--source"; "-s" ],
      [ "jspm"; "skypack"; "unpkg"; "jsdelivr"; "jspm.system" ],
      description = "CDN that will be used to fetch dependencies from"
    )

  let mode =
    Input.OptionWithStrings(
      [ "--mode"; "-m" ],
      [ "dev"; "development"; "prod"; "production" ],
      description = "Restore Dependencies based on the mode to run"
    )

  let buildArgs
    (
      ctx: InvocationContext,
      source: string option,
      mode: string option
    ) : RestoreOptions * CancellationToken =
    { source = source |> Option.map Provider.FromString
      mode = mode |> Option.map RunConfiguration.FromString },
    ctx.GetCancellationToken()

  command "regenerate" {
    addAlias "restore"
    description
      "Restore the import map based on the selected mode, defaults to production"

    inputs (Input.Context(), source, mode)
    setHandler (buildArgs >> Handlers.runRestoreImportMap)
  }

let Template =
  let repoName =
    Input.Argument(
      "templateRepositoryName",
      "The User/repository name combination"
    )

  let newTemplate =
    Input.OptionMaybe(
      [ "--add"; "-a" ],
      "Adds the template repository to Perla"
    )

  let update =
    Input.OptionMaybe(
      [ "--update"; "-u" ],
      "If it exists, updates the template repository for Perla"
    )

  let remove =
    Input.OptionMaybe(
      [ "--remove"; "-r" ],
      "If it exists, removes the template repository for Perla"
    )

  let display =
    Input.OptionWithStrings(
      [ "--list"; "-ls" ],
      [ "table"; "simple" ],
      defaultValue = "table",
      description = "The chosen format to display the existing templates"
    )

  let buildArgs
    (
      ctx: InvocationContext,
      name: string,
      add: bool option,
      update: bool option,
      remove: bool option,
      format: string option
    ) : TemplateRepositoryOptions * CancellationToken =
    let operation =
      let remove =
        remove
        |> Option.map (function
          | true -> Some RunTemplateOperation.Remove
          | false -> None)
        |> Option.flatten

      let update =
        update
        |> Option.map (function
          | true -> Some RunTemplateOperation.Update
          | false -> None)
        |> Option.flatten

      let add =
        add
        |> Option.map (function
          | true -> Some RunTemplateOperation.Add
          | false -> None)
        |> Option.flatten

      let format =
        format
        |> Option.map (function
          | "table" -> RunTemplateOperation.List ListFormat.HumanReadable
          | _ -> RunTemplateOperation.List ListFormat.TextOnly)
        |> Option.defaultValue (
          RunTemplateOperation.List ListFormat.HumanReadable
        )

      remove
      |> Option.orElse update
      |> Option.orElse add
      |> Option.defaultValue format

    { fullRepositoryName = name
      operation = operation },
    ctx.GetCancellationToken()

  let template =
    command "templates" {
      addAlias "t"
      description
        "Handles Template Repository operations such as list, add, update, and remove templates"

      inputs (Input.Context(), repoName, newTemplate, update, remove, display)
      setHandler (buildArgs >> Handlers.runTemplate)
    }

  template

let NewProject =
  let name = Input.Argument("name", "Name of the new project")

  let templateName =
    Input.Option(
      [ "-tn"; "--template-name" ],
      "repository/directory combination of the template name, or the full name in case of name conflicts username/repository/directory"
    )

  let byId =
    Input.Option(
      [ "-id"; "--group-id" ],
      "fully.qualified.name of the template, e.g. perla.templates.vanilla.js"
    )

  let byShortName =
    Input.Option([ "-t"; "--template" ], "shortname of the template, e.g. ff")

  let buildArgs
    (
      ctx: InvocationContext,
      name: string,
      template: string option,
      byId: string option,
      byShortName: string option
    ) : ProjectOptions * CancellationToken =
    { projectName = name
      byTemplateName = template
      byId = byId
      byShortName = byShortName },
    ctx.GetCancellationToken()

  command "new" {
    addAliases ["n"; "create"; "generate"]
    description
      "Creates a new project based on the selected template if it exists"

    inputs (Input.Context(), name, templateName, byId, byShortName)
    setHandler (buildArgs >> Handlers.runNew)
  }

let Test =

  let browsers =
    Input.MultipleStrings(
      "--browser",
      [ "chromium"; "firefox"; "webkit"; "edge"; "chrome" ],
      "Which browsers to run the tests on, defaults to 'chromium'"
    )

  let files: HandlerInput<string[]> =
    Input.Option<string[]>(
      [ "--tests"; "-t" ],
      [||],
      "Specify a glob of tests to run. e.g '**/featureA/*.test.js' or 'tests/my-test.test.js'"
    )

  let skips: HandlerInput<string[]> =
    Input.Option<string[]>(
      [ "--skip"; "-s" ],
      [||],
      "Specify a glob of tests to skip. e.g '**/featureA/*.test.js' or 'tests/my-test.test.js'"
    )


  let headless: HandlerInput<bool option> =
    Input.OptionMaybe<bool>(
      [ "--headless"; "-hl" ],
      "Turn on or off the Headless mode and open the browser (useful for debugging tests)"
    )

  let watch: HandlerInput<bool option> =
    Input.OptionMaybe<bool>(
      [ "--watch"; "-w" ],
      "Start the server and keep watching for file changes"
    )

  let sequential: HandlerInput<bool option> =
    Input.OptionMaybe<bool>(
      [ "--browser-sequential"; "-bs" ],
      "Run each browser's test suite in sequence, rather than parallel"
    )

  let buildArgs
    (
      ctx: InvocationContext,
      browsers: string array option,
      files: string array,
      skips: string array,
      headless: bool option,
      watch: bool option,
      sequential: bool option
    ) : TestingOptions * CancellationToken =
    { browsers =
        browsers
        |> Option.map (fun browsers ->
          if browsers |> Array.isEmpty then
            None
          else
            Some(browsers |> Seq.map Browser.FromString))
        |> Option.flatten
      files = if files |> Array.isEmpty then None else Some files
      skip = if skips |> Array.isEmpty then None else Some skips
      headless = headless
      watch = watch
      browserMode =
        sequential
        |> Option.map (fun sequential ->
          if sequential then Some BrowserMode.Sequential else None)
        |> Option.flatten },
    ctx.GetCancellationToken()

  command "test" {
    description "Runs client side tests in a headless browser"

    inputs (
      Input.Context(),
      browsers,
      files,
      skips,
      headless,
      watch,
      sequential
    )

    setHandler (buildArgs >> Handlers.runTesting)
  }

let Describe =
  let perlaProperties: HandlerInput<string[] option> =
    Arg<string[] option>(
      "properties",
      (fun (result: ArgumentResult) ->
        match result.Tokens |> Seq.toArray with
        | [||] -> None
        | others -> Some(others |> Array.map (fun token -> token.Value))),
      Description =
        "A property, properties or json path-like string names to describe",
      Arity = ArgumentArity.ZeroOrMore
    )
    |> HandlerInput.OfArgument

  let describeCurrent: HandlerInput<bool> =
    Input.Option(
      [ "--current"; "-c" ],
      false,
      "Take my current perla.json file and print my current configuration"
    )

  let buildArgs (properties: string[] option, current: bool) =
    { properties = properties
      current = current }

  command "describe" {
    addAlias "ds"
    description "Describes the perla.json file or it's properties as requested"

    inputs (perlaProperties, describeCurrent)
    setHandler (buildArgs >> Handlers.runDescribePerla)
  }
