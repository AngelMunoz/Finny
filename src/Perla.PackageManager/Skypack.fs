module Perla.PackageManager.Skypack

#nowarn "3391"

open System
open System.Runtime.InteropServices
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading.Tasks
open FsHttp
open FsHttp.MimeTypes
open System.Net.Http.Headers
open System.Collections.Generic

[<Struct>]
type PackageMaintainer = { name: string; email: string }

type PackageSearchResult = {
  createdAt: DateTime
  description: string
  hasTypes: bool
  isDeprecated: bool
  maintainers: PackageMaintainer seq
  name: string
  popularityScore: float
  updatedAt: DateTime
}

[<Struct>]
type PackageCheck = {
  title: string
  pass: bool option
  url: string
}

[<Struct>]
type PackageSearchMeta = {
  page: int
  resultsPerPage: int
  time: int64
  totalCount: int64
  totalPages: int
}

type PackageSearchResults = {
  meta: PackageSearchMeta
  results: PackageSearchResult seq
}

type PackageInfo = {
  name: string
  versions: Map<string, DateTime>
  maintainers: PackageMaintainer seq
  license: string
  projectType: string
  distTags: Map<string, string>
  keywords: string seq
  updatedAt: DateTime
  links: Map<string, string> seq
  qualityScore: float
  createdAt: DateTime
  buildStatus: string
  registry: string
  readmeHtml: string
  description: string
  popularityScore: float
  isDeprecated: bool
  dependenciesCount: int
}

[<Struct>]
type ImportUrls = {
  name: string
  packageUri: Uri
  productionUri: Uri
  developmentUri: Uri
  typescriptTypes: Uri option
}

exception PackageNotFoundException of string

let options = JsonSerializerOptions(IncludeFields = true)

type Skypack =

  static member PackageInfo(name: string) : Task<PackageInfo> = task {
    let! res =
      http { GET $"{Constants.SKYPACK_API}/package/{name}" }
      |> Request.sendTAsync

    return! Response.deserializeJsonWithTAsync options res
  }

  static member SearchPackage
    (
      name: string,
      [<Optional>] ?page: int
    ) : Task<PackageSearchResults> =
    task {
      let page = defaultArg page 1

      let! res =
        http {
          GET $"{Constants.SKYPACK_API}/search"
          query [ "name", name; "page", page ]
        }
        |> Request.sendTAsync

      return! Response.deserializeJsonWithTAsync options res
    }

  static member PackageUrls
    (
      name: string,
      [<Optional>] ?minified: bool,
      [<Optional>] ?esVersion: string,
      [<Optional>] ?typescriptTypes: bool
    ) =
    task {
      let inline (=>) (name: string) object = name, object :> obj

      let minified = defaultArg minified false

      let typescriptTypes = defaultArg typescriptTypes false
      let esVersion = defaultArg esVersion ""

      let! response =
        http {
          GET Constants.SKYPACK_CDN

          query [
            if minified then
              "min" => ""
            if not (String.IsNullOrWhiteSpace esVersion) then
              "dist" => esVersion
            if typescriptTypes then
              "dts" => ""
          ]
        }
        |> Request.sendTAsync

      let baseUri = Uri(Constants.SKYPACK_CDN)

      let packageUri = Uri(baseUri, $"./{name}")

      let tryPickFirst (name: string) (headers: HttpResponseHeaders) =
        headers
        |> Seq.tryPick (fun v ->
          if v.Key = name then v.Value |> Seq.tryHead else None)

      let pinnedUrl =
        match response.headers |> tryPickFirst "x-pinned-url" with
        | Some path -> Uri(baseUri, path)
        | None -> Uri(packageUri.ToString())

      let importUrl =
        match response.headers |> tryPickFirst "x-import-url" with
        | Some path -> Uri(baseUri, path)
        | None -> Uri(packageUri.ToString())

      let typescriptTypes =
        if typescriptTypes then
          response.headers
          |> tryPickFirst "x-typescript-types"
          |> Option.map (fun path -> Uri(baseUri, path))
        else
          None

      return {
        name = name
        packageUri = packageUri
        productionUri = pinnedUrl
        developmentUri = importUrl
        typescriptTypes = typescriptTypes
      }
    }
