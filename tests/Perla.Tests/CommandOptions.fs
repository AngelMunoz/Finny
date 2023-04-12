namespace Perla.Tests

open Xunit
open System
open System.CommandLine
open FSharp.SystemCommandLine.Inputs
open System.CommandLine.Parsing
open System.CommandLine.Invocation
open System.CommandLine.Builder

open FsToolkit.ErrorHandling

open Perla
open Perla.Commands

[<AutoOpen>]
module Extensions =

  type HandlerInput<'a> with

    member this.GetArgument<'T>() : Argument<'T> option =
      match this.Source with
      | ParsedArgument a -> a :?> Argument<'T> |> Some
      | _ -> None

    member this.GetOption<'T>() : Option<'T> option =
      match this.Source with
      | ParsedOption o -> o :?> Option<'T> |> Some
      | _ -> None

    member this.GetValue(ctx: ParseResult) : 'T option =
      match this.Source with
      | ParsedOption o ->
        o :?> Option<'T> |> ctx.GetValueForOption |> Option.ofNull
      | ParsedArgument a ->
        a :?> Argument<'T> |> ctx.GetValueForArgument |> Option.ofNull
      | Context -> None


module CommandOptions =

  let GetRootCommand (handler: Command, command: string) =
    let root = RootCommand()
    root.AddCommand(handler)
    root.Parse(command)

  [<Fact>]
  let ``Commands.Setup can parse options`` () =
    let result =
      GetRootCommand(Commands.Setup, "setup -y --skip-playwright false")

    let skipPlaywright: bool option =
      SetupInputs.skipPlaywright.GetValue result |> Option.flatten

    let skipPrompts: bool option =
      SetupInputs.skipPrompts.GetValue result |> Option.flatten

    Assert.Empty(result.Errors)
    Assert.True(skipPrompts |> Option.defaultValue false)
    Assert.False(skipPlaywright |> Option.defaultValue true)

  [<Fact>]
  let ``Parse Commands.Setup without options should not fail`` () =
    let result = GetRootCommand(Commands.Setup, "setup")

    let skipPlaywright: bool option =
      SetupInputs.skipPlaywright.GetValue result |> Option.flatten

    let skipPrompts: bool option =
      SetupInputs.skipPrompts.GetValue result |> Option.flatten

    Assert.Empty(result.Errors)
    Assert.True(skipPrompts |> Option.isNone)
    Assert.True(skipPlaywright |> Option.isNone)

  [<Fact>]
  let ``Commands.Build can parse options`` () =
    let result =
      GetRootCommand(
        Commands.Build,
        "build --dev -epl false -rim --preview false"
      )

    let asDev: bool option =
      SharedInputs.asDev.GetValue result |> Option.flatten

    let enablePreloads: bool option =
      BuildInputs.enablePreloads.GetValue result |> Option.flatten

    let rebuildImportMap: bool option =
      BuildInputs.rebuildImportMap.GetValue result |> Option.flatten

    let preview: bool option =
      BuildInputs.preview.GetValue result |> Option.flatten


    Assert.Empty(result.Errors)
    Assert.True(asDev.Value)
    Assert.False(enablePreloads.Value)
    Assert.True(rebuildImportMap.Value)
    Assert.False(preview.Value)
