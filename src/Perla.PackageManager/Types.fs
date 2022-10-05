namespace Perla.PackageManager

open System.Runtime.InteropServices
open System.Collections.Generic
open System.Runtime.CompilerServices

[<Extension>]
type DictionaryExtensions =

  [<Extension>]
  static member ToSeq(dictionary: Dictionary<'TKey, 'TValue>) =
    seq { for KeyValue (key, value) in dictionary -> key, value }

module Types =

  type ImportMap =
    { imports: Map<string, string>
      scopes: Map<string, Map<string, string>> option }

    static member CreateMap
      (
        imports: Dictionary<string, string>,
        [<Optional>] ?scopes: Dictionary<string, Dictionary<string, string>>
      ) =
      let imports = imports.ToSeq() |> Map.ofSeq

      let scopes =
        match scopes with
        | Some scopes ->
          seq {
            for KeyValue (scopeKey, scopeValue) in scopes ->
              scopeKey, scopeValue.ToSeq() |> Map.ofSeq
          }
          |> Map.ofSeq
          |> Some
        | None -> None

      { imports = imports; scopes = scopes }

  [<Struct>]
  type GeneratorEnv =
    | Browser
    | Development
    | Production
    | Module
    | Node
    | Deno

    member this.AsString =
      match this with
      | Browser -> "browser"
      | Development -> "development"
      | Production -> "production"
      | Module -> "module"
      | Node -> "node"
      | Deno -> "deno"

  [<Struct>]
  type Provider =
    | Jspm
    | Skypack
    | Unpkg
    | Jsdelivr
    | JspmSystem

    member this.AsString =
      match this with
      | Jspm -> "jspm"
      | Skypack -> "skypack"
      | Unpkg -> "unpkg"
      | Jsdelivr -> "jsdelivr"
      | JspmSystem -> "jspm.system"

module Constants =
  [<Literal>]
  let JSPM_API = "https://api.jspm.io/generate"

  [<Literal>]
  let SKYPACK_CDN = "https://cdn.skypack.dev"

  [<Literal>]
  let SKYPACK_API = "https://api.skypack.dev/v1"

[<AutoOpen>]
module TypeExtensions =
  open Types

  [<Extension>]
  type ImportMapExtensions =

    [<Extension>]
    static member WithImports
      (
        map: ImportMap,
        imports: Dictionary<string, string>
      ) =
      { map with imports = imports.ToSeq() |> Map.ofSeq }

    [<Extension>]
    static member WithScopes
      (
        map: ImportMap,
        [<Optional>] ?scopes: Dictionary<string, Dictionary<string, string>>
      ) =
      let scopes =
        match scopes with
        | Some scopes ->
          seq {
            for KeyValue (scopeKey, scopeValue) in scopes ->
              scopeKey, scopeValue.ToSeq() |> Map.ofSeq
          }
          |> Map.ofSeq
          |> Some
        | None -> None

      { map with scopes = scopes }
