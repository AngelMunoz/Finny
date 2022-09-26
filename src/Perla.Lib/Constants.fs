[<RequireQualifiedAccess>]
module Constants

open System

[<Literal>]
let Esbuild_Version = "0.15.7"

[<Literal>]
let Esbuild_Target = "es2020"

[<Literal>]
let Default_Templates_Repository = "AngelMunoz/perla-templates"

[<Literal>]
let Default_Templates_Repository_Branch = "main"

[<Literal>]
let PerlaConfigName = "perla.jsonc"

[<Literal>]
let ProxyConfigName = "proxy-config.json"

[<Literal>]
let ScaffoldConfiguration = "TemplateConfiguration"

[<Literal>]
let DefaultFableProject = "./src/App.fsproj"

[<Literal>]
let DefaultIndexFile = "index.html"

[<Literal>]
let DefaultPort = 7331

[<Literal>]
let DefaultEnvPath = "/env.js"

[<Literal>]
let PerlaJsonSchemaURL =
  "https://raw.githubusercontent.com/AngelMunoz/Perla/main/perla.schema.json"

let DefaultMount = "./src", "src"
