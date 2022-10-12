namespace Perla

module Units =

  [<Measure>]
  type Semver

  [<Measure>]
  type LocalPath

  [<Measure>]
  type FileExtension

  [<Measure>]
  type ServerPath


module Types =
  open FSharp.UMX
  open Units
  open Perla.PackageManager.Types

  [<RequireQualifiedAccess; Struct>]
  type LoaderType =
    | Typescript
    | Tsx
    | Jsx

  [<RequireQualifiedAccess; Struct>]
  type ReloadKind =
    | FullReload
    | HMR

  [<RequireQualifiedAccess; Struct>]
  type PerlaScript =
    | LiveReload
    | Worker
    | Env

  [<RequireQualifiedAccess>]
  type ReloadEvents =
    | FullReload of string
    | ReplaceCSS of string
    | CompileError of string

    member this.AsString =
      match this with
      | FullReload data -> $"event:reload\ndata:{data}\n\n"
      | ReplaceCSS data -> $"event:replace-css\ndata:{data}\n\n"
      | CompileError err -> $"event:compile-err\ndata:{err}\n\n"

  [<Struct; RequireQualifiedAccess>]
  type RunConfiguration =
    | Production
    | Development

    member this.AsString =
      match this with
      | Production -> "production"
      | Development -> "development"

  type FableConfig =
    { project: string<LocalPath>
      extension: string<FileExtension>
      sourceMaps: bool
      outDir: string<LocalPath> option }

  type DevServerConfig =
    { port: int
      host: string
      liveReload: bool
      useSSL: bool }

  type BuildConfig =
    { esBuildPath: string<LocalPath>
      esbuildVersion: string<Semver>
      includes: string seq
      excludes: string seq
      ecmaVersion: string
      outDir: string<LocalPath>
      minify: bool
      injects: string seq
      externals: string seq
      fileLoaders: Map<string, string>
      emitEnvFile: bool
      jsxFactory: string option
      jsxFragment: string option }

  [<Struct>]
  type Dependency =
    { name: string
      version: string
      alias: string option }

  type PerlaConfig =
    { index: string<LocalPath>
      provider: Provider
      runConfiguration: RunConfiguration
      fable: FableConfig option
      mountDirectories: Map<string, string<LocalPath>>
      enableEnv: bool
      envPath: string<ServerPath>
      devServer: DevServerConfig
      build: BuildConfig
      dependencies: Dependency seq
      devDependencies: Dependency seq }

  exception CommandNotParsedException of string
  exception HelpRequestedException
  exception MissingPackageNameException
  exception MissingImportMapPathException
  exception PackageNotFoundException
  exception HeaderNotFoundException of string
  exception FailedToParseNameException of string

  type RunConfiguration with

    static member FromString(value: string) =
      match value.ToLowerInvariant() with
      | "production"
      | "prod" -> Production
      | "development"
      | "dev"
      | _ -> Development
