namespace CalceTypes

open System
open System.Threading.Tasks

type PluginApi =
  | Stable
  | Next

type PluginError =
  | Load of string
  | ShouldTransform of string
  | Transform of string
  | InjectImports of string

type Runtime =
  | Build
  | DevServer

type FileExtension =
  | JS
  | CSS
  | HTML
  | Custom of string

  member this.AsString =
    match this with
    | JS -> ".js"
    | CSS -> ".css"
    | HTML -> ".html"
    | Custom ext -> ext

  static member FromString ext =
    match ext with
    | ".js" -> JS
    | ".css" -> CSS
    | ".html" -> HTML
    | other -> Custom other

type LoadArgs =
    { runtime: Runtime;
      path: string;
      filePaths: string array;
      tryReadContent: string -> Result<string, string> }

type LoadResult =
  { content: string; targetExtension: FileExtension }

type OnLoad = LoadArgs -> Result<LoadArgs, PluginError>

type ShouldTransformArgs =
  { runtime: Runtime;
    extension: FileExtension
    content: string; }

type OnShouldTransform = ShouldTransformArgs -> Result<bool, PluginError>

type TransformArgs =
  { runtime: Runtime; content: string; currentExtension: FileExtension }

type TransformResult =
  { content: string; targetExtension: FileExtension;  }

type OnTransform = TransformArgs -> Result<TransformResult, PluginError>

type InjectImportsResult =
  { imports: Map<string, string>; scopes: Map<string, Map<string, string>> }

type OnInjectImports = Runtime -> Result<InjectImportsResult, PluginError>

type PluginInfo =
    { name: string
      pluginApi: PluginApi
      load: OnLoad option
      shouldTransform: OnShouldTransform option
      transform: OnTransform option
      injectImports: OnInjectImports option }
