[<AutoOpen>]
module Perla.Lib.Extensions


open System
open System.Diagnostics
open System.IO

module Constants =
  [<Literal>]
  let Esbuild_Version = "0.14.1"

  [<Literal>]
  let Default_Templates_Repository = "AngelMunoz/perla-samples"

  [<Literal>]
  let Default_Templates_Repository_Branch = "main"

  [<Literal>]
  let PerlaConfigName = "perla.jsonc"

  [<Literal>]
  let ProxyConfigName = "proxy-config.json"

  [<Literal>]
  let ScaffoldConfiguration = "TemplateConfiguration"

type Path with

  static member PerlaRootDirectory =
    let assemblyLoc =
      Path.GetDirectoryName(Reflection.Assembly.GetEntryAssembly().Location)

    if String.IsNullOrWhiteSpace assemblyLoc then
      Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)
    else
      assemblyLoc

  static member LocalDBPath =
    Path.Combine(Path.PerlaRootDirectory, "templates.db")

  static member TemplatesDirectory =
    Path.Combine(Path.PerlaRootDirectory, "templates")

  static member GetPerlaConfigPath(?directoryPath: string) =
    let rec findConfigFile currDir =
      let path = Path.Combine(currDir, Constants.PerlaConfigName)

      if File.Exists path then
        Some path
      else
        match Path.GetDirectoryName currDir |> Option.ofObj with
        | Some parent ->
          if parent <> currDir then
            findConfigFile parent
          else
            None
        | None -> None

    let workDir = defaultArg directoryPath Environment.CurrentDirectory

    findConfigFile (Path.GetFullPath workDir)
    |> Option.defaultValue (Path.Combine(workDir, Constants.PerlaConfigName))

  static member GetProxyConfigPath(?directoryPath: string) =
    $"{defaultArg directoryPath (Environment.CurrentDirectory)}/{Constants.ProxyConfigName}"

  static member SetCurrentDirectoryToPerlaConfigDirectory() =
    Path.GetPerlaConfigPath()
    |> Path.GetDirectoryName
    |> Directory.SetCurrentDirectory
