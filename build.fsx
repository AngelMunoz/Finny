#!/usr/bin/env -S dotnet fsi
#r "nuget: Fake.DotNet.Cli, 5.20.4"
#r "nuget: Fake.IO.FileSystem, 5.20.4"
#r "nuget: Fake.Core.Target, 5.20.4"
#r "nuget: Fake.DotNet.MsBuild, 5.20.4"
#r "nuget: MSBuild.StructuredLogger, 2.1.507"
#r "nuget: System.IO.Compression.ZipFile, 4.3.0"
#r "nuget: System.Reactive"

open System
open System.IO.Compression
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

// https://github.com/fsharp/FAKE/issues/2517#issuecomment-727282959
Environment.GetCommandLineArgs()
|> Array.skip 2 // skip fsi.exe; build.fsx
|> Array.toList
|> Context.FakeExecutionContext.Create false "build.fsx"
|> Context.RuntimeContext.Fake
|> Context.setExecutionContext

let output = "./dist"

let runtimes =
    [| "linux-x64"
       "linux-arm64"
       "win10-x64"
       "osx-x64" |]

Target.initEnvironment ()
Target.create "Clean" (fun _ -> !! "dist" |> Shell.cleanDirs)

Target.create
    "PackNugets"
    (fun _ ->
        DotNet.pack
                (fun opts ->
                    { opts with
                          Configuration = DotNet.BuildConfiguration.Release
                          OutputPath = Some $"{output}" })
                "src/Perla")

Target.create
    "BuildBinaries"
    (fun _ ->
        let args = MSBuild.CliArguments.Create()

        let getOpts (runtime: string) (opts: DotNet.PublishOptions) =
            { opts with
                  SelfContained = Some true
                  Runtime = Some runtime
                  Configuration = DotNet.BuildConfiguration.Release
                  OutputPath = Some $"{output}/{runtime}"
                  MSBuildParams =
                      { args with
                            Properties = [ "PublishSingleFile", "true" ] } }

        let getPublishCmd getOpts runtime =
            DotNet.publish (getOpts runtime) "src/Perla/Perla.fsproj"

        Array.iter (getPublishCmd getOpts) runtimes)

Target.create
    "Zip"
    (fun _ ->
        runtimes
        |> Array.Parallel.iter
            (fun runtime -> ZipFile.CreateFromDirectory($"{output}/{runtime}", $"{output}/{runtime}.zip")))

Target.create "Default" (fun _ -> Target.runSimple "Zip" [] |> ignore)

"Clean" ==> "BuildBinaries" ==> "PackNugets" ==> "Default"

Target.runOrDefault "Default"
