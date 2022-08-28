namespace CalceTypes

open System
open System.Threading.Tasks

type OnLoadArgs =
    { url: Uri
      source: string
      loader: string }

type ResolveArgs =
  { filepath: string; }

type LoadArgs =
  { filepath: string; }

type LoadResult =
  { content: string; extension: string }

type TransformArgs =
  { content: string; path: string }

type TransformResult =
  { content: string; }

type OnResolveCallback = ResolveArgs -> bool
type OnLoadCallback = LoadArgs -> LoadResult
type OnTransformCallback = TransformArgs -> TransformResult

type PluginInfo =
    { name: string
      extension: string
      resolve: OnResolveCallback option
      load: OnLoadCallback option
      transform: OnTransformCallback option }
