namespace Perla.CliMiddleware

open System
open System.Threading.Tasks
open System.CommandLine.Invocation

open LiteDB

open FSharp.UMX

open Perla
open Perla.Logger
open Perla.Handlers
open Perla.FileSystem
open Perla.Constants
open Perla.Types
open Perla.Units
open Spectre.Console

type internal MiddlewareFn =
  InvocationContext -> Func<InvocationContext, Task> -> Task

type internal SetupChecks = {
  isAlreadySetUp: unit -> bool
  saveSetup: unit -> ObjectId
}

type internal EsbuildBinCheck = {
  esbuildVersion: string<Semver>
  isEsbuildPresent: string<Semver> -> bool
  saveEsbuildPresent: string<Semver> -> ObjectId
}

module internal MiddlewareImpl =
  open FsToolkit.ErrorHandling

  [<Struct>]
  type SetupError =
    | SetupFailed
    | SetupCheckSave

  let previewCheck: MiddlewareFn =
    fun context next -> task {
      let cmd = context.ParseResult.CommandResult.Command

      let enablePreview =
        context.ParseResult.Directives.Contains CliDirectives.Preview

      let isHidden = context.ParseResult.CommandResult.Command.IsHidden

      if isHidden then
        Logger.log ("[bold red]This command is in preview.[/]", escape = false)

      if isHidden && not enablePreview then
        Logger.log (
          "[orange1]To enable preview commands, run with the [bold yellow][[preview]][/] directive.[/]",
          escape = false
        )

        Logger.log (
          $"[orange1]perla [yellow][[preview]][/] {cmd.Name} --command --options[/]",
          escape = false
        )

        context.ExitCode <- 1
        return ()

      if isHidden && enablePreview then
        Logger.log (
          "[orange1]And may perform unexpected actions or not work at all.[/]",
          escape = false
        )

        return! next.Invoke context

      return! next.Invoke context
    }

  let esbuildPluginCheck (config: PerlaConfig) : MiddlewareFn =
    fun context next -> task {
      let cmd = context.ParseResult.CommandResult.Command

      let missingEsbuildPlugin =
        not (config.plugins |> List.contains "perla-esbuild-plugin")

      let disableEsbuildPluginWarning =
        context.ParseResult.Directives.Contains CliDirectives.NoEsbuildPlugin

      if missingEsbuildPlugin && not disableEsbuildPluginWarning then
        Logger.log (
          "The [bold yellow]perla-esbuild-plugin[/] plugin was not found in your plugin list.",
          escape = false
        )

        Logger.log (
          "Keep in mind that this plugin is required for both the [bold yellow]build[/] and [bold yellow]serve[/] commands to work.",
          escape = false
        )

        Logger.log
          "To disable this warning run with the [no-esbuild-plugin] directive"

        Logger.log (
          $"[yellow]perla [[no-esbuild-plugin]] {cmd.Name}[/] --command --options",
          escape = false
        )

      return! next.Invoke context
    }

  let setupCheck (checks: SetupChecks) : MiddlewareFn =
    fun context next -> task {
      let isInCI = context.ParseResult.Directives.Contains CliDirectives.CiRun

      let isSetupCmd =
        context.ParseResult.CommandResult.Command.Name.Equals "setup"

      let isAlreadySetup = checks.isAlreadySetUp ()

      if isSetupCmd || isAlreadySetup then
        return! next.Invoke context
      else
        Logger.log "Looks like this is your first time using perla..."
        Logger.log "Setting up perla for the first time..."

        let! setupResult =
          Handlers.runSetup (
            {
              skipPrompts = isInCI
              installTemplates = not isInCI
            },
            context.GetCancellationToken()
          )

        let savedChecks =
          setupResult
          |> Result.requireEqualTo 0 SetupFailed
          |> Result.bind (fun _ ->
            checks.saveSetup () |> Result.requireNotNull SetupCheckSave)
          |> Result.ignore

        match savedChecks with
        | Ok _ -> return! next.Invoke context
        | Error SetupFailed ->
          Logger.log "Failed to setup perla for the first time..."
          context.ExitCode <- 1
          return ()
        | Error SetupCheckSave ->
          Logger.log
            "We couldn't save some internal checks, this won't affect your project but it's recommended to report this issue."

          return! next.Invoke context
    }

  let esbuildBinCheck (checks: EsbuildBinCheck) : MiddlewareFn =
    fun context next -> task {
      let isInCI = context.ParseResult.Directives.Contains CliDirectives.CiRun
      let cmdName = context.ParseResult.CommandResult.Command.Name

      let isEsbuildRequired =
        [ "serve"; "build"; "test" ] |> List.contains cmdName

      let isEsbuildPresent = checks.isEsbuildPresent checks.esbuildVersion

      if not isEsbuildRequired || isEsbuildPresent then
        return! next.Invoke context
      else if isInCI then
        try
          do! FileSystem.SetupEsbuild(checks.esbuildVersion)
          checks.saveEsbuildPresent checks.esbuildVersion |> ignore
          return! next.Invoke context
        with ex ->
          Logger.log ("Failed to setup esbuild! We can't continue", ex = ex)
          context.ExitCode <- 1
          return ()
      else if
        AnsiConsole.Confirm("Do you want to setup esbuild right now?", true)
      then
        do! FileSystem.SetupEsbuild(checks.esbuildVersion)
        checks.saveEsbuildPresent checks.esbuildVersion |> ignore
        return! next.Invoke context
      else
        Logger.log "Skipping esbuild setup..."
        return! next.Invoke context
    }

  let templatesCheck
    (
      templatesArePresent: unit -> bool,
      setCheck: unit -> ObjectId
    ) : MiddlewareFn =
    fun context next -> task {
      let isNewCmd = context.ParseResult.CommandResult.Command.Name.Equals "new"
      let areTemplatesPresent = templatesArePresent ()

      if not isNewCmd || areTemplatesPresent then
        return! next.Invoke context
      else
        Logger.log
          "Looks like you don't have the default perla templates installed..."

        Logger.log "Installing default perla templates..."

        let! result =
          Handlers.runTemplate (
            {
              fullRepositoryName =
                $"{Default_Templates_Repository}:{Default_Templates_Repository_Branch}"
              operation = RunTemplateOperation.Add
            },
            context.GetCancellationToken()
          )

        if result = 0 then
          Logger.log "Successfully installed default perla templates..."
          setCheck () |> ignore
          return! next.Invoke context

        Logger.log "Failed to install templates, it is not possible continue..."

        Logger.log
          "Please try again, if this keeps happening please report this issue."

        context.ExitCode <- 1
        return ()
    }

  let fableCheck (isFableInConfig: bool) : MiddlewareFn =
    fun context next -> task {
      let cmdName = context.ParseResult.CommandResult.Command.Name

      let isFableRequired =
        [ "serve"; "build"; "test" ] |> List.contains cmdName

      if not isFableRequired || not isFableInConfig then
        return! next.Invoke context
      else
        let! isFablePresent = FileSystem.CheckFableExists()

        if isFablePresent then
          return! next.Invoke context
        else
          Logger.log
            "Looks like you don't have fable installed, we'll try to call [yellow]dotnet tool restore[/]."

          match! FileSystem.DotNetToolRestore() with
          | Ok() -> return! next.Invoke context
          | Error err ->
            Logger.log "dotnet tool restore failed:"
            Logger.log err
            Logger.log "Please try installing fable manually and try again."
            context.ExitCode <- 1
            return ()

    }

  let runDotEnv: MiddlewareFn =
    fun context next -> task {
      let cmdName = context.ParseResult.CommandResult.Command.Name

      let runDotEnv = [ "serve"; "build"; "test" ] |> List.contains cmdName

      if runDotEnv then
        FileSystem.GetDotEnvFilePaths() |> Env.LoadEnvFiles

      return! next.Invoke context
    }

[<RequireQualifiedAccess>]
module Middleware =
  open Perla.Configuration
  open Perla.Database

  let PreviewCheck = InvocationMiddleware(MiddlewareImpl.previewCheck)

  let EsbuildPluginCheck =
    ConfigurationManager.UpdateFromFile()
    let config = ConfigurationManager.CurrentConfig
    InvocationMiddleware(MiddlewareImpl.esbuildPluginCheck config)

  let SetupCheck =
    let checks: SetupChecks = {
      isAlreadySetUp = Checks.IsSetupPresent
      saveSetup = Checks.SaveSetup
    }

    InvocationMiddleware(MiddlewareImpl.setupCheck checks)

  let EsbuildBinCheck =
    ConfigurationManager.UpdateFromFile()
    let config = ConfigurationManager.CurrentConfig

    let checks: EsbuildBinCheck = {
      esbuildVersion = config.esbuild.version
      isEsbuildPresent = Checks.IsEsbuildBinPresent
      saveEsbuildPresent = Checks.SaveEsbuildBinPresent
    }

    InvocationMiddleware(MiddlewareImpl.esbuildBinCheck checks)

  let TemplatesCheck =
    InvocationMiddleware(
      MiddlewareImpl.templatesCheck (
        Checks.AreTemplatesPresent,
        Checks.SaveTemplatesPresent
      )
    )

  let FableCheck =
    ConfigurationManager.UpdateFromFile()
    let config = ConfigurationManager.CurrentConfig

    InvocationMiddleware(MiddlewareImpl.fableCheck config.fable.IsSome)

  let RunDotEnv = InvocationMiddleware(MiddlewareImpl.runDotEnv)
