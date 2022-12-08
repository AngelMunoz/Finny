namespace Perla

[<AutoOpen>]
module Lib =
  open System
  open Perla.Types
  open Perla.PackageManager.Types
  open Spectre.Console
  open System.Text.RegularExpressions

  let internal (|ParseRegex|_|) (regex: Regex) str =
    let m = regex.Match(str)

    if m.Success then
      Some(List.tail [ for x in m.Groups -> x.Value ])
    else
      None

  let ExtractDependencyInfoFromUrl url =

    match url with
    | ParseRegex (Regex(@"https://cdn.skypack.dev/pin/(@?[^@]+)@v([\d.]+)(-?\w+(?!\w+)[\d.]?[\d+]+)?")) [ name
                                                                                                          version
                                                                                                          preview ] ->
      Some(Provider.Skypack, name, $"{version}{preview}")
    | ParseRegex (Regex(@"https://cdn.jsdelivr.net/npm/(@?[^@]+)@([\d.]+)(-?[-]\w+[\d.]?[\d+]+)?")) [ name
                                                                                                      version
                                                                                                      preview ] ->
      Some(Provider.Jsdelivr, name, $"{version}{preview}")
    | ParseRegex (Regex(@"https://ga.jspm.io/npm:(@?[^@]+)@([\d.]+)(-?[-]\w+[\d.]?[\d+]+)?")) [ name
                                                                                                version
                                                                                                preview ] ->
      Some(Provider.Jspm, name, $"{version}{preview}")
    | ParseRegex (Regex(@"https://unpkg.com/(@?[^@]+)@([\d.]+)(-?[-]\w+[\d.]?[\d+]+)?")) [ name
                                                                                           version
                                                                                           preview ] ->
      Some(Provider.Unpkg, name, $"{version}{preview}")
    | _ -> None

  let parseFullRepositoryName (value: string) =
    let regex = new Regex(@"^([-_\w\d]+)\/([-_\w\d]+):?([\w\d-_]+)?$")

    match value with
    | ParseRegex regex [ username; repository; branch ] ->
      Some(username, repository, branch)
    | ParseRegex regex [ username; repository ] ->
      Some(username, repository, "main")
    | _ -> None

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
        defaultArg dependency.version "",
        defaultArg dependency.alias ""
      )
      |> ignore

    table

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

  let (|Log|Debug|Info|Err|Warning|Clear|) level =
    match level with
    | "assert"
    | "debug" -> Debug
    | "info" -> Info
    | "error" -> Err
    | "warning" -> Warning
    | "clear" -> Clear
    | "log"
    | "dir"
    | "dirxml"
    | "table"
    | "trace"
    | "startGroup"
    | "startGroupCollapsed"
    | "endGroup"
    | "profile"
    | "profileEnd"
    | "count"
    | "timeEnd"
    | _ -> Log
