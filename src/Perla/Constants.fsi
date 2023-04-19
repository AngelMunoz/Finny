module Perla.Constants

[<Literal>]
val Esbuild_Version: string = "0.17.15"

[<Literal>]
val Esbuild_Target: string = "es2020"

[<Literal>]
val Default_Templates_Repository: string = "AngelMunoz/perla-templates"

[<Literal>]
val Default_Templates_Repository_Branch: string = "main"

[<Literal>]
val PerlaConfigName: string = "perla.json"

[<Literal>]
val IndexFile: string = "index.html"

[<Literal>]
val FableProject: string = "./src/App.fsproj"

[<Literal>]
val EnvPath: string = "/env.js"

[<Literal>]
val ProxyConfigName: string = "proxy-config.json"

[<Literal>]
val ScaffoldConfiguration: string = "TemplateConfiguration"

[<Literal>]
val TemplatesDatabase: string = "templates.db"

[<Literal>]
val TemplatesDirectory: string = "templates"

[<Literal>]
val ImportMapName: string = "perla.json.importmap"

[<Literal>]
val TemplatingScriptName: string = "templating.fsx"

[<Literal>]
val JsonSchemaUrl: string = "https://raw.githubusercontent.com/AngelMunoz/Perla/main/perla.schema.json"

[<Literal>]
val PerlaEsbuildPluginName: string = "perla-esbuild-plugin"

[<Literal>]
val ArtifactsDirectoryname: string = "perla"

module CliDirectives =
  [<Literal>]
  val Preview: string = "preview"

  [<Literal>]
  val NoEsbuildPlugin: string = "no-esbuild-plugin"

  [<Literal>]
  val CiRun: string = "ci-run"
