module Perla.Json

open System
open System.Text.Json
open System.Text.Json.Serialization
open System.Text.Json.Nodes
open Perla.Types

[<RequireQualifiedAccess; Struct>]
type PerlaConfigSection =
  | Index of index: string option
  | Fable of fable: FableConfig option
  | DevServer of devServer: DevServerConfig option
  | Build of build: BuildConfig option
  | Dependencies of dependencies: Dependency seq option
  | DevDependencies of devDependencies: Dependency seq option

let DefaultJsonOptions () =
  JsonSerializerOptions(
    WriteIndented = true,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    UnknownTypeHandling = JsonUnknownTypeHandling.JsonElement,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
  )

let DefaultJsonNodeOptions () =
  JsonNodeOptions(PropertyNameCaseInsensitive = true)

let DefaultJsonDocumentOptions () =
  JsonDocumentOptions(
    AllowTrailingCommas = true,
    CommentHandling = JsonCommentHandling.Skip
  )

type Json =
  static member ToBytes value =
    JsonSerializer.SerializeToUtf8Bytes(value, DefaultJsonOptions())

  static member FromBytes<'T>(value: byte array) =
    JsonSerializer.Deserialize<'T>(ReadOnlySpan value, DefaultJsonOptions())

  static member ToText(value, ?minify) =
    let opts = DefaultJsonOptions()
    let minify = defaultArg minify false
    opts.WriteIndented <- minify
    JsonSerializer.Serialize(value, opts)

  static member ToNode value =
    JsonSerializer.SerializeToNode(value, DefaultJsonOptions())
