open System
open System.Collections.Generic
open System.IO.Compression
open System.Text
open System.Text.Json
open FSharp.Control
open System.Text.Json.Nodes
open System.Text.Json.Serialization
open System.Threading.Tasks
open System.IO
open Flurl
open Flurl.Http
open ICSharpCode.SharpZipLib.GZip
open Perla.Lib.Types
open Perla.Lib
open FsToolkit.ErrorHandling
open Spectre.Console
open ICSharpCode.SharpZipLib.Tar

[<CLIMutable>]
type NpmDist =
  { fileCount: int64
    integrity: string
    shasum: string
    tarball: string
    unpackedSize: int64
    [<JsonExtensionData>]
    extras: JsonObject }

[<CLIMutable>]
type NpmPackageListing =
  { name: string
    version: string
    types: string option
    dist: NpmDist option
    // like package name, version, author
    // and other package.json fields
    [<JsonExtensionData>]
    extras: JsonObject }

let obtainsPackageMetadata (packages: seq<Source * string * string>) =
  asyncSeq {
    for (_, name, version) in packages do
      try
        use! stream =
          $"https://registry.npmjs.org/{name}/{version}"
            .GetAsync()
            .ReceiveStream()
          |> Async.AwaitTask

        let! result =
          (JsonSerializer.DeserializeAsync<NpmPackageListing> stream)
            .AsTask()
          |> Async.AwaitTask

        match result.types with
        | Some _ -> yield result
        | None -> ()
      with
      | ex ->
        Logger.Logger.log (
          $"Failet to fetch information about {name}@{version}",
          ex
        )
  }
  |> AsyncSeq.toListAsync

let downloadTarFile (packageName: string) (version: string) (dist: NpmDist) =
  async {
    let dir = Directory.CreateDirectory($"./perla-deps").CreateSubdirectory(packageName).CreateSubdirectory(version)
    let! file = dist.tarball.GetStreamAsync() |> Async.AwaitTask
    let tar = TarArchive.CreateInputTarArchive(new GZipInputStream(file), Encoding.UTF8)
    tar.ExtractContents(dir.FullName, true)
    return dir.FullName
  }
let downloadTarFiles (packages: NpmPackageListing list) =
  asyncSeq {
     for package in packages do
        match package.dist with
        | Some file ->
            try
              let! path = downloadTarFile package.name package.version file
              yield path, package
            with ex ->
              Logger.Logger.log("Failed to decompress a file", ex)
        | None -> ()
  }
  |> AsyncSeq.toListAsync

let packages =
  result {
    let! config =
      Fs.getPerlaConfig "./perla.jsonc"
      |> Result.setError "Failed to get config"

    let packages =
      option {
        let! packages = config.packages

        return
          packages
          |> Map.values
          |> Seq.choose (fun value ->
            match parseUrl value with
            | Some version -> version |> Some
            | None -> None)
      }

    return!
      packages
      |> Result.requireSome "No packages were found in config"
  }

let copyFile (filepath: string) =
    let pkgIndex = filepath.IndexOf("package")

    let newPath = filepath.Remove(pkgIndex, "package".Length).Replace("perla-deps", "perla-deps/types")
    Path.GetDirectoryName newPath
    |> Directory.CreateDirectory
    |> ignore
    try
      File.Copy(filepath, newPath)
    with ex ->
      printfn "%s" ex.Message

taskResult {
  let! packages = packages

  let! operations =
    Logger.Logger.spinner (
      "fetching dependencies metadata",
      obtainsPackageMetadata packages
    )

  let! downloads = Logger.Logger.spinner("Downloading Packages", downloadTarFiles operations)
  Logger.Logger.log($"Downloaded: %A{downloads}".EscapeMarkup())

  for (path, package) in downloads do
      Directory.GetFiles(path, "*?.d.ts", EnumerationOptions(RecurseSubdirectories = true))
      |> Array.Parallel.iter copyFile

}
|> Async.AwaitTask
|> Async.Ignore
|> Async.RunSynchronously
