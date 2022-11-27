#r "nuget: CliWrap, 3.5.0"
#r "nuget: Flurl.Http, 3.2.4"
#r "nuget: SharpZipLib, 1.4.1"

open System
open System.IO
open System.Threading.Tasks
open CliWrap
open Flurl.Http
open ICSharpCode.SharpZipLib.GZip
open ICSharpCode.SharpZipLib.Tar

let EsbuildBinaryPath () : string =
    "/home/runner/.local/share/perla/package/bin/esbuild" |> Path.GetFullPath

let chmodBinCmd () =
    Cli
        .Wrap("chmod")
        .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
        .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
        .WithArguments($"+x {EsbuildBinaryPath()}")

let tryDownloadEsBuild () : Task<string option> =
    let url =
        "https://registry.npmjs.org/esbuild-linux-64/-/esbuild-linux-64-0.15.11.tgz"

    let compressedFile = "/home/runner/.local/share/perla/esbuild.tgz"
    compressedFile |> Path.GetDirectoryName |> Directory.CreateDirectory |> ignore

    task {
        try
            use! stream = url.GetStreamAsync()
            Console.WriteLine $"Downloading esbuild from: {url}"

            use file = File.OpenWrite(compressedFile)

            do! stream.CopyToAsync(file)

            Console.WriteLine $"Downloaded esbuild to: {file.Name}"

            return Some(file.Name)
        with ex ->
            Console.Error.WriteLine($"Failed to download esbuild from: {url}", ex)
            return None
    }

let decompressEsbuild (path: Task<string option>) =
    task {
        match! path with
        | Some path ->

            use stream = new GZipInputStream(File.OpenRead path)

            use archive = TarArchive.CreateInputTarArchive(stream, Text.Encoding.UTF8)

            path |> Path.GetDirectoryName |> archive.ExtractContents

            Console.WriteLine $"Executing: chmod +x on \"{EsbuildBinaryPath()}\""
            let res = chmodBinCmd().ExecuteAsync()
            do! res.Task :> Task

            Console.WriteLine "Cleaning up!"

            File.Delete(path)

            Console.WriteLine "This setup should happen once per machine"
            Console.WriteLine "If you see it often please report a bug."
        | None -> ()

        return ()
    }

tryDownloadEsBuild ()
|> decompressEsbuild
|> Async.AwaitTask
|> Async.RunSynchronously
