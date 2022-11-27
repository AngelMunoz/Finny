module Perla.PackageManager.Skypack

#nowarn "3391"

open System
open System.Runtime.InteropServices
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading.Tasks
open Flurl
open Flurl.Http
open Perla.PackageManager.Types

[<Struct>]
type PackageMaintainer = { name: string; email: string }

type PackageSearchResult =
  { createdAt: DateTime
    description: string
    hasTypes: bool
    isDeprecated: bool
    maintainers: PackageMaintainer seq
    name: string
    popularityScore: float
    updatedAt: DateTime }

[<Struct>]
type PackageCheck =
  { title: string
    pass: bool option
    url: string }

[<Struct>]
type PackageSearchMeta =
  { page: int
    resultsPerPage: int
    time: int64
    totalCount: int64
    totalPages: int }

type PackageSearchResults =
  { meta: PackageSearchMeta
    results: PackageSearchResult seq
    [<JsonExtensionData>]
    extras: Map<string, JsonElement> }

type PackageInfo =
  { name: string
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
    [<JsonExtensionData>]
    extras: Map<string, JsonElement> }

[<Struct>]
type ImportUrls =
  { name: string
    packageUri: Uri
    productionUri: Uri
    developmentUri: Uri
    typescriptTypes: Uri option }

exception PackageNotFoundException of string

type Skypack =

  static member PackageInfo(name: string) : Task<PackageInfo> =
    task {
      use! res =
        Constants
          .SKYPACK_API
          .AppendPathSegments("package", name)
          .GetStreamAsync()

      return! JsonSerializer.DeserializeAsync<PackageInfo>(res)
    }

  static member SearchPackage(name: string, [<Optional>] ?page: int) =
    task {
      let page = defaultArg page 1

      use! res =
        Constants
          .SKYPACK_API
          .AppendPathSegment("search")
          .SetQueryParams({| q = name; p = page |})
          .GetStreamAsync()

      return! JsonSerializer.DeserializeAsync<PackageSearchResults>(res)
    }

  static member PackageUrls
    (
      name: string,
      [<Optional>] ?minified: bool,
      [<Optional>] ?esVersion: string,
      [<Optional>] ?typescriptTypes: bool
    ) =
    task {
      let minified = defaultArg minified false

      let typescriptTypes = defaultArg typescriptTypes false

      let! response =
        let url = Constants.SKYPACK_CDN

        let url = if minified then url.SetQueryParam("min") else url

        let url =
          match esVersion with
          | Some esVersion -> url.SetQueryParam(esVersion)
          | None -> url

        let url = if typescriptTypes then url.SetQueryParam("dts") else url

        url.GetAsync()

      let baseUri = Uri(Constants.SKYPACK_CDN)

      let packageUri = Uri(Constants.SKYPACK_CDN).AppendPathSegment(name)

      let pinnedUrl =
        match response.Headers.TryGetFirst("x-pinned-url") with
        | true, path -> Uri(baseUri, path)
        | false, _ -> Uri(packageUri.ToString())

      let importUrl =
        match response.Headers.TryGetFirst("x-import-url") with
        | true, path -> Uri(baseUri, path)
        | false, _ -> Uri(packageUri.ToString())

      let typescriptTypes =
        if typescriptTypes then
          match response.Headers.TryGetFirst("x-typescript-types") with
          | true, path -> Some(Uri(baseUri, path))
          | false, _ -> None
        else
          None

      return
        { name = name
          packageUri = packageUri.ToUri()
          productionUri = pinnedUrl
          developmentUri = importUrl
          typescriptTypes = typescriptTypes }
    }
