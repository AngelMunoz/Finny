module Perla.PackageManager.Jspm

open System.Runtime.InteropServices
open System.Threading.Tasks
open System.Text.Json
open FsHttp
open FsHttp.MimeTypes
open System.Text.Json.Serialization
open Perla.PackageManager.Types
open System.Net

type FileDependencies = {
  staticDeps: string seq option
  dynamicDeps: string seq option
}

type File = Map<string, FileDependencies>

type DependencyGraph = Map<string, File>

type InstallResponse = {
  staticDeps: string seq
  dynamicDeps: string seq
  map: ImportMap
  graph: DependencyGraph option
} with

  member this.ToJson
    ([<Optional; DefaultParameterValue false>] ?indented: bool)
    =
    let indented = defaultArg indented false

    if indented then
      JsonSerializer.Serialize(
        this,
        JsonSerializerOptions(WriteIndented = true)
      )
    else
      JsonSerializer.Serialize(this)

  member this.ToHtml
    (
      [<Optional>] ?esModulesShim: bool,
      [<Optional>] ?indentMap: bool,
      [<Optional>] ?preload: bool
    ) =
    let esModulesShim = defaultArg esModulesShim false

    let indentMap = defaultArg indentMap false
    let preload = defaultArg preload false

    let esModulesShim =
      if esModulesShim then
        """<script async src="https://ga.jspm.io/npm:es-module-shims@1.5.3/dist/es-module-shims.js"></script>"""
      else
        ""

    let map =
      let map =
        JsonSerializer.Serialize(
          this.map,
          JsonSerializerOptions(WriteIndented = indentMap)
        )

      $"""<script type="importmap">{map}</script>"""

    let preload =
      if preload then
        this.staticDeps
        |> Seq.fold
          (fun current next ->
            $"{current}\n<link rel=\"modulepreload\" href=\"{next}\" />")
          ""
      else
        ""

    let imports =
      this.map.imports
      |> Map.fold
        (fun current nextKey _ ->
          let importName =
            nextKey.Replace("-", "").Replace(".", "").Replace("@", "")

          $"{current}\nimport {importName} from \"{nextKey}\"")
        ""

    $"""<!doctype html>
<html>
<head>
<meta charset="utf-8">
<title>Untitled</title>
<meta name="viewport" content="width=device-width, initial-scale=1">
</head>
<body>
{map}
{esModulesShim}
{preload}
<script type="module">{imports}
</script>
</body>
</html>
"""

type JspmGenerator =

  static member Install
    (
      packages: string seq,
      [<Optional>] ?environments: GeneratorEnv seq,
      [<Optional>] ?provider: Provider,
      [<Optional>] ?inputMap: ImportMap,
      [<Optional>] ?flattenScope: bool,
      [<Optional>] ?graph: bool
    ) : Task<Result<InstallResponse, string>> =
    let payload = {|
      install = packages |> Seq.toArray
      env =
        environments
        |> Option.map (fun envs -> envs |> Seq.map (fun e -> e.AsString))
      provider = provider |> Option.map (fun p -> p.AsString)
      inputMap = inputMap
      flattenScope = flattenScope
      graph = graph
    |}

    let opts =
      JsonSerializerOptions(
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
      )

    task {
      let serialized = JsonSerializer.Serialize(payload, opts)

      let! req =
        http {
          POST Constants.JSPM_API
          body
          json serialized
        }
        |> Request.sendTAsync

      if req.statusCode <> HttpStatusCode.OK then
        let! result = req |> Response.deserializeJsonTAsync<{| error: string |}>

        return Error result.error
      else
        let! result =
          Response.deserializeJsonWithTAsync<{|
            staticDeps: string seq option
            dynamicDeps: string seq option
            map: ImportMap option
            graph: DependencyGraph option
          |}>
            opts
            req

        return
          Ok {
            staticDeps = defaultArg result.staticDeps Seq.empty
            dynamicDeps = defaultArg result.dynamicDeps Seq.empty
            map =
              match result.map with
              | Some map -> map
              | None -> { imports = Map.empty; scopes = None }
            graph = result.graph

          }
    }
