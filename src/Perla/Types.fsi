namespace Perla
open System

module Units =
    [<Measure>]
    type Semver

    [<Measure>]
    type SystemPath

    [<Measure>]
    type FileExtension

    [<Measure>]
    type ServerUrl

    [<Measure>]
    type UserPath

module Types =
    open FSharp.UMX
    open Units
    open Perla.PackageManager.Types

    [<Struct; RequireQualifiedAccess>]
    type RunConfiguration =
        | Production
        | Development

        member AsString: string

    type FableConfig =
        { project: string<SystemPath>
          extension: string<FileExtension>
          sourceMaps: bool
          outDir: string<SystemPath> option }

    type DevServerConfig =
        { port: int
          host: string
          liveReload: bool
          useSSL: bool
          proxy: Map<string, string> }

    type EsbuildConfig =
        { esBuildPath: string<SystemPath>
          version: string<Semver>
          ecmaVersion: string
          minify: bool
          injects: string seq
          externals: string seq
          fileLoaders: Map<string, string>
          jsxFactory: string option
          jsxFragment: string option }

    type BuildConfig =
        { includes: string seq
          excludes: string seq
          outDir: string<SystemPath>
          emitEnvFile: bool }

    type Dependency =
        { name: string
          version: string option
          alias: string option }

        member internal AsVersionedString: string

    type PerlaConfig =
        { index: string<SystemPath>
          runConfiguration: RunConfiguration
          provider: Provider
          build: BuildConfig
          devServer: DevServerConfig
          fable: FableConfig option
          esbuild: EsbuildConfig
          mountDirectories: Map<string<ServerUrl>, string<UserPath>>
          enableEnv: bool
          envPath: string<ServerUrl>
          dependencies: Dependency seq
          devDependencies: Dependency seq }

    type Test =
        { body: string
          duration: float option
          fullTitle: string
          id: string
          pending: bool
          speed: string option
          state: string option
          title: string
          ``type``: string }

    type Suite =
        { id: string
          title: string
          fullTitle: string
          root: bool
          parent: string option
          pending: bool
          tests: Test list }

    type TestStats =
        { suites: int
          tests: int
          passes: int
          pending: int
          failures: int
          start: DateTime
          ``end``: DateTime option }

    type TestEvent =
        | SessionStart of stats: TestStats * totalTests: int
        | SessionEnd of stats: TestStats
        | SuiteStart of stats: TestStats * suite: Suite
        | SuiteEnd of stats: TestStats * suite: Suite
        | TestPass of stats: TestStats * test: Test
        | TestFailed of stats: TestStats * test: Test * message: string * stack: string
        | TestImportFailed of message: string * stack: string
        | TestRunFinished

    exception CommandNotParsedException of string
    exception HelpRequestedException
    exception MissingPackageNameException
    exception MissingImportMapPathException
    exception PackageNotFoundException
    exception HeaderNotFoundException of string
    exception FailedToParseNameException of string

    type RunConfiguration with

        static member FromString: value: string -> RunConfiguration
