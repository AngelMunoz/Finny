module Perla.PackageManager.Skypack

open System.Text.Json
open System.Text.Json.Serialization
open System.Threading.Tasks

[<Struct>]
type PackageMaintainer = { name: string; email: string }

type PackageSearchResult = {
  createdAt: System.DateTime
  description: string
  hasTypes: bool
  isDeprecated: bool
  maintainers: seq<PackageMaintainer>
  name: string
  popularityScore: float
  updatedAt: System.DateTime
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
  results: seq<PackageSearchResult>
  [<JsonExtensionData>]
  extras: Map<string, JsonElement>
}

type PackageInfo = {
  name: string
  versions: Map<string, System.DateTime>
  maintainers: seq<PackageMaintainer>
  license: string
  projectType: string
  distTags: Map<string, string>
  keywords: seq<string>
  updatedAt: System.DateTime
  links: seq<Map<string, string>>
  qualityScore: float
  createdAt: System.DateTime
  buildStatus: string
  registry: string
  readmeHtml: string
  description: string
  popularityScore: float
  isDeprecated: bool
  dependenciesCount: int
  [<JsonExtensionData>]
  extras: Map<string, JsonElement>
}

[<Struct>]
type ImportUrls = {
  name: string
  packageUri: System.Uri
  productionUri: System.Uri
  developmentUri: System.Uri
  typescriptTypes: System.Uri option
}

exception PackageNotFoundException of string

[<Class>]
type Skypack =

  /// Request the information of a particular package of the Skypack V1 API
  static member PackageInfo: name: string -> Task<PackageInfo>

  /// <summary>
  /// Requests the skypack specific import URLs, this is useful to switch between
  /// development and production ready assets.
  /// </summary>
  /// <param name="name">The package to obtain urls for</param>
  /// <param name="minified">Should the sources be minified</param>
  /// <param name="esVersion">The Ecmascript version to use to ensure compatibility. Example: es2020, es2016, es2019</param>
  /// <param name="typescriptTypes">Include the Uri for the typescript types for the package</param>
  static member PackageUrls:
    name: string * ?minified: bool * ?esVersion: string * ?typescriptTypes: bool ->
      Task<ImportUrls>

  /// Makes an http call to the skypack api to search possible package candidates for the given name
  static member SearchPackage:
    name: string * ?page: int -> Task<PackageSearchResults>
