namespace CalceTypes

open System
open System.Threading.Tasks

type CurrentStage =
    | Load
    | Build

type OnBuildArgs = { source: string; target: string }
type OnBuildResult = { content: byte []; extension: string }
type OnBuildCallback = OnBuildArgs -> Task<OnBuildResult>

type OnCopyArgs = { source: string; target: string }
type OnCopyResult = { content: byte []; extension: string }
type OnCopyCallback = OnCopyArgs -> Task<OnCopyResult>

type OnVirtualizeArgs = { stage: CurrentStage }

type OnVirtualizeResult =
    { content: byte []
      extension: string
      mimeType: string
      url: string
      path: string }

type OnVirtualizeCallback = OnVirtualizeArgs -> Task<OnVirtualizeResult>

type OnLoadArgs =
    { url: Uri
      source: string
      loader: string }

type OnLoadResult = { content: byte []; mimeType: string }
type OnLoadCallback = OnLoadArgs -> Task<OnLoadResult>


type PluginInfo =
    { name: string
      build: OnBuildCallback option
      copy: OnCopyCallback option
      virtualize: OnVirtualizeCallback option
      load: OnLoadCallback option }
