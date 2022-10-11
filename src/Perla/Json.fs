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
  | Devserver of devServer: DevServerConfig option
  | Build of build: BuildConfig option
  | Dependencies of dependencies: Dependency seq option
  | DevDependencies of devDependencies: Dependency seq option

let private jsonOptions () =
  JsonSerializerOptions(
    WriteIndented = true,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    UnknownTypeHandling = JsonUnknownTypeHandling.JsonElement,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
  )


type Json =
  static member ToBytes value =
    JsonSerializer.SerializeToUtf8Bytes(value, jsonOptions ())

  static member FromBytes<'T>(value: byte array) =
    JsonSerializer.Deserialize<'T>(ReadOnlySpan value, jsonOptions ())

  static member ToText(value, ?minify) =
    let opts = jsonOptions ()
    let minify = defaultArg minify false
    opts.WriteIndented <- minify
    JsonSerializer.Serialize(value, opts)

  static member ToPackageJson dependencies =
    JsonSerializer.Serialize({| dependencies = dependencies |}, jsonOptions ())

  static member WritePerlaSection
    (
      section: PerlaConfigSection,
      content: byte array
    ) =
    let node =
      JsonNode.Parse(
        ReadOnlySpan content,
        JsonNodeOptions(PropertyNameCaseInsensitive = true),
        JsonDocumentOptions(
          AllowTrailingCommas = true,
          CommentHandling = JsonCommentHandling.Skip
        )
      )

    match section with
    | PerlaConfigSection.Index value ->
      node["index"] <- JsonSerializer.SerializeToNode(value)
    | PerlaConfigSection.Fable value ->
      node["fable"] <- JsonSerializer.SerializeToNode(value)
    | PerlaConfigSection.Devserver value ->
      node["devServer"] <- JsonSerializer.SerializeToNode(value)
    | PerlaConfigSection.Build value ->
      node["build"] <- JsonSerializer.SerializeToNode(value)
    | PerlaConfigSection.Dependencies value ->
      node["dependencies"] <- JsonSerializer.SerializeToNode(value)
    | PerlaConfigSection.DevDependencies value ->
      node["devDependencies"] <- JsonSerializer.SerializeToNode(value)

    node.Deserialize<Types.PerlaConfig>()

  static member WritePerlaSections
    (
      sections: PerlaConfigSection seq,
      content: byte array
    ) =
    let node =
      JsonNode.Parse(
        ReadOnlySpan content,
        JsonNodeOptions(PropertyNameCaseInsensitive = true),
        JsonDocumentOptions(
          AllowTrailingCommas = true,
          CommentHandling = JsonCommentHandling.Skip
        )
      )

    for section in sections do
      match section with
      | PerlaConfigSection.Index value ->
        node["index"] <- JsonSerializer.SerializeToNode(value)
      | PerlaConfigSection.Fable value ->
        node["fable"] <- JsonSerializer.SerializeToNode(value)
      | PerlaConfigSection.Devserver value ->
        node["devServer"] <- JsonSerializer.SerializeToNode(value)
      | PerlaConfigSection.Build value ->
        node["build"] <- JsonSerializer.SerializeToNode(value)
      | PerlaConfigSection.Dependencies value ->
        node["dependencies"] <- JsonSerializer.SerializeToNode(value)
      | PerlaConfigSection.DevDependencies value ->
        node["devDependencies"] <- JsonSerializer.SerializeToNode(value)

    node.Deserialize<Types.PerlaConfig>()
