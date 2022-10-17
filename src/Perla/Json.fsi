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

val DefaultJsonOptions: unit -> JsonSerializerOptions
val DefaultJsonNodeOptions: unit -> JsonNodeOptions
val DefaultJsonDocumentOptions: unit -> JsonDocumentOptions

[<Class>]
type Json =
    static member ToBytes: value: 'a -> byte array
    static member FromBytes<'T> : value: byte array -> 'T
    static member ToText: value: 'a * ?minify: bool -> string
    static member ToNode: value: 'a -> JsonNode
