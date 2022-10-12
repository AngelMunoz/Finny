namespace Perla

[<AutoOpen>]
module Lib =
  open System
  open System.Text
  open Perla.Types
  open Perla.PackageManager.Types
  open Perla.Scaffolding
  open Spectre.Console

  let (|ParseRegex|_|) regex str =
    let m = RegularExpressions.Regex(regex).Match(str)

    if m.Success then
      Some(List.tail [ for x in m.Groups -> x.Value ])
    else
      None

  let parseUrl url =
    match url with
    | ParseRegex @"https://cdn.skypack.dev/pin/(@?[^@]+)@v([\d.]+)"
                 [ name; version ] -> Some(Provider.Skypack, name, version)
    | ParseRegex @"https://cdn.jsdelivr.net/npm/(@?[^@]+)@([\d.]+)"
                 [ name; version ] -> Some(Provider.Jsdelivr, name, version)
    | ParseRegex @"https://ga.jspm.io/npm:(@?[^@]+)@([\d.]+)" [ name; version ] ->
      Some(Provider.Jspm, name, version)
    | ParseRegex @"https://unpkg.com/(@?[^@]+)@([\d.]+)" [ name; version ] ->
      Some(Provider.Unpkg, name, version)
    | _ -> None

  let (|RestartFable|StartFable|StopFable|UnknownFable|) =
    function
    | "restart:fable" -> RestartFable
    | "start:fable" -> StartFable
    | "stop:fable" -> StopFable
    | value -> UnknownFable value

  let (|RestartServer|StartServer|StopServer|Clear|Exit|Unknown|) =
    function
    | "restart" -> RestartServer
    | "start" -> StartServer
    | "stop" -> StopServer
    | "clear"
    | "cls" -> Clear
    | "exit"
    | "stop" -> Exit
    | value -> Unknown value

  let (|Typescript|Javascript|Jsx|Css|Json|Other|) value =
    match value with
    | ".ts"
    | ".tsx" -> Typescript
    | ".js" -> Javascript
    | ".jsx" -> Jsx
    | ".json" -> Json
    | ".css" -> Css
    | _ -> Other value



  let (|ScopedPackage|Package|) (package: string) =
    if package.StartsWith("@") then
      ScopedPackage(package.Substring(1))
    else
      Package package

  let parsePackageName (name: string) =

    let getVersion parts =

      let version =
        let version = parts |> Seq.tryLast |> Option.defaultValue ""

        if String.IsNullOrWhiteSpace version then
          None
        else
          Some version

      version

    match name with
    | ScopedPackage name ->
      // check if the user is looking to install a particular version
      // i.e. package@5.0.0
      if name.Contains("@") then
        let parts = name.Split("@")
        let version = getVersion parts

        $"@{parts.[0]}", version
      else
        $"@{name}", None
    | Package name ->
      if name.Contains("@") then
        let parts = name.Split("@")

        let version = getVersion parts
        parts.[0], version
      else
        name, None

  let getRepositoryName (fullRepoName: string) =
    match
      fullRepoName.Split("/") |> Array.filter (String.IsNullOrWhiteSpace >> not)
    with
    | [| _; repoName |] -> Ok repoName
    | [| _ |] -> Error MissingRepoName
    | _ -> Error WrongGithubFormat

  let getTemplateAndChild (templateName: string) =
    match
      templateName.Split("/") |> Array.filter (String.IsNullOrWhiteSpace >> not)
    with
    | [| user; template; child |] -> Some user, template, Some child
    | [| template; child |] -> None, template, Some child
    | [| template |] -> None, template, None
    | _ -> None, templateName, None

  let dependencyTable (deps: Dependency seq, title: string) =
    let table = Table().AddColumns([| "Name"; "Version"; "Alias" |])
    table.Title <- TableTitle(title)

    for column in table.Columns do
      column.Alignment <- Justify.Left

    for dependency in deps do
      table.AddRow(
        dependency.name,
        dependency.version,
        defaultArg dependency.alias ""
      )
      |> ignore

    table
