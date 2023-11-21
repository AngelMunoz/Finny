module Perla.Plugins.Tests.Library

open System
open System.Threading.Tasks

open Xunit

open IcedTasks
open Perla.Plugins

[<Fact>]
let ``"plugin" can be made with a synchronous function`` () = taskUnit {
  let plugin = plugin "plugin" {
    with_transform (fun file -> { file with content = "transformed" })
  }

  let file = { extension = ".txt"; content = "test" }

  Assert.Equal("plugin", plugin.name)

  match plugin.transform with
  | ValueSome transform ->
    let! result = transform file
    Assert.Equal("transformed", result.content)
  | ValueNone -> Assert.Fail("transform should not be none")
}

[<Fact>]
let ``"plugin" can be made with an asynchronous function that returns a task``
  ()
  =
  taskUnit {
    let plugin = plugin "plugin" {
      with_transform (fun file -> task {
        do! Task.Delay(50)
        return { file with content = "transformed" }
      })
    }

    let file = { extension = ".txt"; content = "test" }

    Assert.Equal("plugin", plugin.name)

    match plugin.transform with
    | ValueSome transform ->
      let! result = transform file
      Assert.Equal("transformed", result.content)
    | ValueNone -> Assert.Fail("transform should not be none")
  }

[<Fact>]
let ``"plugin" can be made with an asynchronous function that returns an async``
  ()
  =
  taskUnit {
    let plugin = plugin "plugin" {
      with_transform (fun file -> async {
        do! Async.Sleep(50)
        return { file with content = "transformed" }
      })
    }

    let file = { extension = ".txt"; content = "test" }

    Assert.Equal("plugin", plugin.name)

    match plugin.transform with
    | ValueSome transform ->
      let! result = transform file
      Assert.Equal("transformed", result.content)
    | ValueNone -> Assert.Fail("transform should not be none")
  }

[<Fact>]
let ``"plugin" can be made with an asynchronous function that returns a valuetask``
  ()
  =
  taskUnit {
    let plugin = plugin "plugin" {
      with_transform (fun file -> vTask {
        return { file with content = "transformed" }
      })
    }

    let file = { extension = ".txt"; content = "test" }

    Assert.Equal("plugin", plugin.name)

    match plugin.transform with
    | ValueSome transform ->
      let! result = transform file
      Assert.Equal("transformed", result.content)
    | ValueNone -> Assert.Fail("transform should not be none")
  }

[<Fact>]
let ``"plugin" should process a file if the predicate is true`` () =
  let plugin = plugin "plugin" {
    should_process_file (fun extension -> extension = ".txt")
    with_transform (fun file -> { file with content = "transformed" })
  }

  let file = { extension = ".txt"; content = "test" }

  match plugin.shouldProcessFile with
  | ValueSome shouldProcessFile ->
    let result = shouldProcessFile file.extension
    Assert.True(result)
  | ValueNone -> Assert.Fail("shouldProcessFile should not be none")

[<Fact>]
let ``"plugin" should not process a file if the predicate is false`` () =
  let plugin = plugin "plugin" {
    should_process_file (fun extension -> extension = ".txt")
    with_transform (fun file -> { file with content = "transformed" })
  }

  let file = {
    extension = ".json"
    content = "test"
  }

  match plugin.shouldProcessFile with
  | ValueSome shouldProcessFile ->
    file.extension |> shouldProcessFile |> Assert.False
  | ValueNone -> Assert.Fail("shouldProcessFile should not be none")

[<Fact>]
let ``"plugin" should not process a file if the predicate is not provided`` () =
  let plugin = plugin "plugin" {
    with_transform (fun file -> { file with content = "transformed" })
  }

  match plugin.shouldProcessFile with
  | ValueSome _ -> Assert.Fail("shouldProcessFile should not be none")
  | ValueNone -> Assert.True(true)
