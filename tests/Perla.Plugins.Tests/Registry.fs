module Perla.Plugins.Tests.Registry


open System
open System.Collections.Generic
open System.Threading.Tasks

open Xunit

open FSharp.Compiler.Interactive.Shell
open IcedTasks

open Perla.Plugins
open Perla.Plugins.Registry

let pluginFactory (amount: int) = [
  for i in 1..amount do
    {
      name = sprintf "test%d" i
      shouldProcessFile =
        if i % 2 = 0 then ValueSome(fun _ -> true) else ValueNone
      transform =
        ValueSome(fun file ->
          ValueTask.FromResult(
            {
              file with
                  content = $"--- %i{i}\n%s{file.content}"
            }
          ))
    }
]

module Runnables =
  type RunnableContainer =
    static let plugins =
      lazy
        (Dictionary<string, PluginInfo>(
          [
            for p in pluginFactory 10 do
              KeyValuePair(p.name, p)
          ]
        ))

    static member PluginCache = plugins

  [<Fact>]
  let ``GetRunnables should return only those who have a "should process file" function``
    ()
    =
    let runnables =
      PluginRegistry.GetRunnablePlugins<RunnableContainer>(
        [
          "test1"
          "test2"
          "test3"
          "test4"
          "test5"
          "test6"
          "test7"
          "test8"
          "test9"
          "test10"
        ]
      )

    Assert.Equal(5, runnables.Length)

  [<Fact>]
  let ``GetPluginList should bring all of the plugins in cache`` () =
    let plugins = PluginRegistry.GetPluginList<RunnableContainer>()

    Assert.Equal(10, plugins.Length)

  [<Fact>]
  let ``LoadFromCode should give error if the plugin is already there`` () =
    let plugin = PluginRegistry.GetPluginList<RunnableContainer>()[0]
    let result = PluginRegistry.LoadFromCode<RunnableContainer>(plugin)

    match result with
    | Error(PluginLoadError.AlreadyLoaded _) -> Assert.True(true)
    | Error err -> Assert.Fail $"Expected error, but got %A{err}"
    | Ok() -> Assert.Fail $"Expected error, but got %A{result}"

  [<Fact>]
  let ``LoadFromCode should be successful if the plugin is not in the cache``
    ()
    =
    let plugin = {
      name = "test11"
      shouldProcessFile = ValueNone
      transform = ValueNone
    }

    let result = PluginRegistry.LoadFromCode<RunnableContainer>(plugin)

    match result with
    | Ok() -> Assert.True true
    | Error result -> Assert.Fail $"Expected success, but got %A{result}"
