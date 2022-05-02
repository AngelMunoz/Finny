open System
open System.Collections.Generic
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.Json.Serialization
open System.Threading.Tasks
open System.IO
open Flurl
open Flurl.Http
open Perla.Lib.Types
open Perla.Lib
open FsToolkit.ErrorHandling

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
  { types: string option
    dist: NpmDist option
  // like package name, version, author
  // and other package.json fields
    [<JsonExtensionData>]
    extras: JsonObject
   }

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

taskResult {
  let! packages = packages

  let! operations =
    [ for (_, name, version) in packages do
        task {
          use! stream =
            $"https://registry.npmjs.org/{name}/{version}"
              .GetAsync()
              .ReceiveStream()

          return! JsonSerializer.DeserializeAsync<NpmPackageListing> stream
        } ]
    |> Task.WhenAll

  printfn "%A" operations
}
|> Async.AwaitTask
|> Async.Ignore
|> Async.RunSynchronously
