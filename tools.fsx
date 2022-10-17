#r "nuget: FsMake, 0.6.1"

open FsMake
open System.IO
open System.IO.Compression

let runtimes =
    [| "linux-x64"
       "linux-arm64"
       "osx-x64"
       "osx-arm64"
       "win10-x64"
       "win10-arm64" |]

let fsSources =
    Glob.create "*.fsx"
    |> Glob.toPaths
    |> Seq.append (
        Glob.createWithRootDir "src" "**/*.fs"
        |> Glob.add "**/*.fsi"
        |> Glob.exclude "**/fable_modules/*.fs"
        |> Glob.exclude "**/fable_modules/**/*.fs"
        |> Glob.exclude "**/obj/*.fs"
        |> Glob.exclude "**/obj/**/*.fs"
        |> Glob.toPaths
    )

let outDir = Path.GetFullPath("./dist")

module Operations =
    type FantomasError =
        | PendingFormat
        | FailedToFormat

    let cleanDist =
        make {
            let! ctx = Make.context

            try
                Directory.Delete(outDir, true)
            with ex ->
                ctx.Console.WriteLine(Console.warn $"We tried to delete '{outDir}' but '{ex.Message}' happened")
        }

    let fantomas (command: string) =
        make {
            let! result =
                Cmd.createWithArgs "dotnet" [ "fantomas"; command; yield! fsSources ]
                |> Cmd.checkExitCode Cmd.ExitCodeCheckOption.CheckCodeNone
                |> Cmd.redirectOutput Cmd.RedirectToBoth
                |> Cmd.result

            match result.ExitCode with
            | 0 -> return ()
            | 99 -> return! Make.fail (nameof FantomasError.PendingFormat)
            | _ -> return! Make.fail (nameof FantomasError.FailedToFormat)
        }

    let dotnet (args: string) =
        Cmd.createWithArgs "dotnet" (args.Split(' ') |> List.ofArray) |> Cmd.run

    let buildBinaries (project: string) (runtime: string) =
        let cmd =
            let framework = "net6.0"
            let singleFile = "-p:PublishSingleFile=true"
            let readyToRun = "-p:PublishReadyToRun=true"
            let outdir = $"{outDir}/{runtime}"
            $"publish {project} -c Release -f {framework} -r {runtime} --self-contained {singleFile} {readyToRun} -o {outdir}"

        dotnet cmd

module Steps =
    let installTools =
        Step.create "Install Tools" { do! Operations.dotnet "tool restore" }

    let restore = Step.create "Restore" { do! Operations.dotnet "paket restore" }

    let clean = Step.create "Clean" { do! Operations.cleanDist }

    let packNugets =
        Step.create "Pack Nugets" {
            let! ctx = Step.context
            do! Operations.cleanDist
            Console.info "Generating NuGet Package" |> ctx.Console.WriteLine

            do! Operations.dotnet $"pack src/Perla -o {outDir}"
        }

    let zip =
        Step.create "Zip binaries" {
            let! ctx = Step.context

            Console.info "Zipping Binaries" |> ctx.Console.WriteLine

            runtimes
            |> Array.Parallel.iter (fun runtime ->
                ZipFile.CreateFromDirectory($"{outDir}/{runtime}", $"{outDir}/{runtime}.zip"))

            Console.info $"Binaries Zipped at '{outDir}'" |> ctx.Console.WriteLine
        }

    let buildBin =
        Step.create "Build Binaries" {
            let! ctx = Step.context

            Console.info "Building Binaries" |> ctx.Console.WriteLine

            for runtime in runtimes do
                Console.info $"Starting [{runtime}]" |> ctx.Console.WriteLine

                do! Operations.buildBinaries "src/Perla/Perla.fsproj" runtime
        }

    let buildForRuntime =
        Step.create "Building binary for runtime" {
            let! ctx = Step.context

            match ctx.ExtraArgs |> List.tryHead with
            | Some runtime ->
                Console.info $"Starting [{runtime}]" |> ctx.Console.WriteLine

                do! Operations.buildBinaries "src/Perla/Perla.fsproj" runtime
            | None ->
                [ Console.error "No runtime found in the extra arguments."
                  Console.error "example: dotnet fsi build.fsx build:runtime -- linux-x64" ]
                |> ctx.Console.WriteLines

                return! Step.fail "No runtime found in the extra arguments."
        }

    let build =
        Step.create "build" { do! Operations.dotnet "build src/Perla/Perla.fsproj --no-restore" }

    let format = Step.create "format" { do! Operations.fantomas "format" }

    let test =
        Step.create "tests" { do! Operations.dotnet "test --no-build --no-restore" }

module Pipelines =

    let restore =
        Pipeline.create "restore" {
            run Steps.installTools
            run Steps.restore
        }

    let format = Pipeline.createFrom restore "format" { run Steps.format }

    let packNuget =
        Pipeline.createFrom restore "build:nuget" {
            run Steps.clean
            run Steps.packNugets
        }

    let buildRelease =
        Pipeline.createFrom packNuget "build:release" {
            run Steps.packNugets
            run Steps.buildBin
            run Steps.zip
        }

    let buildRuntime =
        Pipeline.createFrom packNuget "build:runtime" { run Steps.buildForRuntime }

    let build = Pipeline.createFrom restore "build" { run Steps.build }

    let test = Pipeline.createFrom build "test" { run Steps.test }

Pipelines.create {
    add Pipelines.restore
    add Pipelines.format
    add Pipelines.packNuget
    add Pipelines.buildRelease
    add Pipelines.buildRuntime
    add Pipelines.build
    add Pipelines.test
    default_pipeline Pipelines.build
}
|> Pipelines.runWithArgsAndExit fsi.CommandLineArgs
