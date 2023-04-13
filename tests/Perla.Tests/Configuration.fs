namespace Perla.Tests

open Xunit

open Perla
open Perla.Types
open Perla.Logger
open Perla.Configuration

open FsToolkit.ErrorHandling

module Configuration =

  // we're not picking up env variables yet for perla config
  [<Fact>]
  let ``fromEnv returns the same config passed`` () =
    let config = Defaults.PerlaConfig
    Assert.Equal(config, fromEnv config)

  [<Fact>]
  let ``fromFile should update devServer options in perla config`` () =
    let config = Defaults.PerlaConfig

    let configText =
      """{
  "devServer": {
    "port": 3000,
    "host": "0.0.0.0",
    // skip "liveReload" just to ensure the defaults are respected
    "useSSL": false,
    "proxy": { "/reqUrl": "https://target" }
  }
}"""

    let parsedConfig = Json.getConfigDocument configText
    let result = Configuration.fromFile (Some parsedConfig) config

    Assert.Equal(
      result,
      {
        config with
            devServer = {
              port = 3000
              host = "0.0.0.0"
              liveReload = true
              useSSL = false
              proxy = Map.ofList [ "/reqUrl", "https://target" ]
            }
      }
    )
