namespace Perla.Lib

open System
open System.Text.Json
open System.Text.Json.Serialization

module Types =

  [<RequireQualifiedAccess>]
  type LoaderType =
    | Typescript
    | Tsx
    | Jsx

  [<RequireQualifiedAccess>]
  type ReloadKind =
    | FullReload
    | HMR

  [<RequireQualifiedAccess>]
  type PerlaScript =
    | LiveReload
    | Worker
    | Env

  [<RequireQualifiedAccess>]
  type ReloadEvents =
    | FullReload of string
    | ReplaceCSS of string
    | CompileError of string

  type FableConfig =
    { autoStart: bool option
      project: string option
      extension: string option
      outDir: string option }

  type WatchConfig =
    { extensions: string seq option
      directories: string seq option }

  type DevServerConfig =
    { autoStart: bool option
      port: int option
      host: string option
      mountDirectories: Map<string, string> option
      watchConfig: WatchConfig option
      liveReload: bool option
      useSSL: bool option
      enableEnv: bool option
      envPath: string option }

  type CopyPaths =
    { includes: string seq option
      excludes: string seq option }

  type BuildConfig =
    { esBuildPath: string option
      esbuildVersion: string option
      copyPaths: CopyPaths option
      target: string option
      outDir: string option
      bundle: bool option
      format: string option
      minify: bool option
      jsxFactory: string option
      jsxFragment: string option
      injects: string seq option
      externals: string seq option
      fileLoaders: Map<string, string> option
      emitEnvFile: bool option }

  type PerlaConfig =
    { ``$schema``: string option
      index: string option
      fable: FableConfig option
      devServer: DevServerConfig option
      build: BuildConfig option
      packages: Map<string, string> option }

  type Scope = Map<string, string>

  type ImportMap =
    { imports: Map<string, string>
      scopes: Map<string, Scope> }

  type PackagesLock = ImportMap

  type Source =
    | Skypack = 0
    | Jspm = 1
    | Jsdelivr = 2
    | Unpkg = 3

  type InitKind =
    | Full = 0
    | Simple = 1

  type SkypackSearchResult =
    { createdAt: DateTime
      description: string
      hasTypes: bool
      isDeprecated: bool
      maintainers: {| name: string; email: string |} seq
      name: string
      popularityScore: float
      updatedAt: DateTime }

  type ShowSearchResults =
    { name: string
      versions: Map<string, DateTime>
      distTags: Map<string, string>
      maintainers: {| name: string; email: string |} seq
      license: string
      updatedAt: DateTime
      registry: string
      description: string
      isDeprecated: bool
      dependenciesCount: int
      links: Map<string, string> seq }

  type PackageCheck =
    { title: string
      pass: bool option
      url: string }

  type JspmResponse =
    { staticDeps: string seq
      dynamicDeps: string seq
      map: ImportMap }

  type SkypackSearchResponse =
    { meta: {| page: int
               resultsPerPage: int
               time: int
               totalCount: int64
               totalPages: int |}
      results: SkypackSearchResult seq
      [<JsonExtensionData>]
      extras: Map<string, JsonElement> }

  type SkypackPackageResponse =
    { name: string
      versions: Map<string, DateTime>
      maintainers: {| name: string; email: string |} seq
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

  type InitOptions =
    { path: string option
      withFable: bool option
      initKind: InitKind option
      yes: bool option }

  type SearchOptions =
    { package: string option
      page: int option }

  type ShowPackageOptions = { package: string option }

  type AddPackageOptions =
    { package: string option
      alias: string option
      source: Source option }

  type RemovePackageOptions = { package: string option }

  type ListFormat =
    | HumanReadable
    | PackageJson

  type ListPackagesOptions = { format: ListFormat }

  [<CLIMutable>]
  type PerlaTemplateRepository =
    { _id: string
      name: string
      fullName: string
      branch: string
      path: string
      createdAt: DateTime
      updatedAt: Nullable<DateTime> }

  type NameParsingErrors =
    | MissingRepoName
    | WrongGithubFormat

  type RepositoryOptions =
    { fullRepositoryName: string
      branch: string }

  type ProjectOptions =
    { projectName: string
      templateName: string }

  exception CommandNotParsedException of string
  exception HelpRequestedException
  exception MissingPackageNameException
  exception MissingImportMapPathException
  exception PackageNotFoundException
  exception HeaderNotFoundException of string
  exception TemplateNotFoundException of string
  exception FailedToParseNameException of string
  exception AddTemplateFailedException
  exception UpdateTemplateFailedException
  exception DeleteTemplateFailedException
