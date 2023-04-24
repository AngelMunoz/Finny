namespace Perla.CliMiddleware

open System
open System.Collections.Generic
open System.CommandLine
open System.Threading
open System.Threading.Tasks
open System.CommandLine.Invocation

open FsToolkit.ErrorHandling.Operator.Result
open LiteDB

open FSharp.UMX

open FsToolkit.ErrorHandling

open Perla.Logger

open Perla
open Perla.Units
open Perla.Constants
open Perla.Handlers

type Setup =
  abstract IsAlreadySetUp: unit -> bool
  abstract SaveSetup: unit -> ObjectId
  abstract RunSetup: bool * CancellationToken -> Task<int>

type Esbuild =
  abstract EsbuildVersion: string<Semver>
  abstract IsEsbuildPresent: string<Semver> -> bool
  abstract SaveEsbuildPresent: string<Semver> -> ObjectId
  abstract RunSetupEsbuild: string<Semver> * CancellationToken -> Task<unit>

  abstract IsEsbuildPluginAbsent: bool

type Templates =
  abstract TemplatesArePresent: unit -> bool
  abstract SaveTemplatesArePresent: unit -> ObjectId

  abstract RunSetupTemplates:
    TemplateRepositoryOptions * CancellationToken -> Task<int>

type Fable =
  abstract IsFableInConfig: bool
  abstract IsFablePresent: CancellationToken -> Task<bool>
  abstract RestoreFable: CancellationToken -> Task<Result<unit, string>>

type DotEnv =
  abstract GetDotEnvFiles: unit -> string<SystemPath> seq
  abstract LoadDotEnvFiles: string<SystemPath> seq -> unit

type CliMiddlewareEnv =
  abstract Setup: Setup
  abstract Esbuild: Esbuild
  abstract Templates: Templates
  abstract Fable: Fable
  abstract DotEnv: DotEnv

[<RequireQualifiedAccess>]
module Middleware =

  [<Struct>]
  type PerlaMdResult =
    | Continue
    | Exit of int

  [<RequireQualifiedAccess>]
  type PerlaCliMiddleware =
    | AsMiddleware of
      (Command * KeyValuePair<string, string seq> seq
        -> Task<Result<unit, PerlaMdResult>>)
    | AsCancellableMiddleware of
      (Command * KeyValuePair<string, string seq> seq * CancellationToken
        -> Task<Result<unit, PerlaMdResult>>)

  module internal MiddlewareImpl =

    let ShouldRunFor (candidate: string) (commands: string list) =
      if List.contains candidate commands then
        Ok()
      else
        Error Continue

    let HasDirective
      (directive: string)
      (directives: KeyValuePair<string, string seq> seq)
      : bool =
      let comparison (current: KeyValuePair<string, string seq>) =
        current.Key.Equals(
          directive,
          StringComparison.InvariantCultureIgnoreCase
        )

      Seq.exists comparison directives

    let ToSCLMiddleware
      (middleware: PerlaCliMiddleware)
      : InvocationMiddleware =
      InvocationMiddleware(fun context next -> task {
        let command = context.ParseResult.CommandResult.Command
        let directives = context.ParseResult.Directives
        let cancellationToken = context.GetCancellationToken()

        let! result =
          match middleware with
          | PerlaCliMiddleware.AsMiddleware middleware ->
            middleware (command, directives)
          | PerlaCliMiddleware.AsCancellableMiddleware middleware ->
            middleware (command, directives, cancellationToken)

        match result with
        | Ok()
        | Error Continue -> return! next.Invoke context
        | Error(Exit code) ->
          context.ExitCode <- code
          return ()
      })

    let previewCheck
      (
        command: Command,
        directives: KeyValuePair<string, string seq> seq
      ) : Task<Result<unit, PerlaMdResult>> =
      taskResult {
        if command.IsHidden then
          Logger.log (
            "[bold red]This command is in preview.[/]",
            escape = false
          )

          Logger.log (
            "[orange1]It may perform unexpected actions or not work at all.[/]",
            escape = false
          )

          do!
            HasDirective CliDirectives.Preview directives
            |> Result.requireTrue (Exit 1)
            |> Result.ignore

        return ()
      }
      |> TaskResult.teeError (fun _ ->
        Logger.log (
          "[orange1]To enable preview commands, run with the [bold yellow][[preview]][/] directive.[/]",
          escape = false
        )

        Logger.log (
          $"[orange1]perla [yellow][[preview]][/] {command.Name} --command --options[/]",
          escape = false
        ))

    let esbuildPluginCheck
      (env: #Esbuild)
      (command: Command, directives: KeyValuePair<string, string seq> seq)
      : Task<Result<unit, PerlaMdResult>> =
      taskResult {
        do! ShouldRunFor command.Name [ "build"; "serve"; "test" ]

        let hasEsbuildPluginDirective =
          HasDirective CliDirectives.NoEsbuildPlugin directives


        if env.IsEsbuildPluginAbsent then
          Logger.log (
            "The [bold yellow]{Constants.PerlaEsbuildPluginName}[/] plugin was not found in your plugin list.",
            escape = false
          )

        match env.IsEsbuildPluginAbsent, hasEsbuildPluginDirective with
        | false, _
        | true, true -> return ()
        | true, false -> return! Error(Exit 1)

      }
      |> TaskResult.teeError (fun error ->
        match error with
        | Continue -> ()
        | Exit _ ->
          Logger.log
            "To disable this warning run with the [no-esbuild-plugin] directive"

          Logger.log (
            $"[yellow]perla [[no-esbuild-plugin]] {command.Name}[/] --command --options",
            escape = false
          ))

    let setupCheck
      (env: #Setup)
      (command: Command,
       directives: KeyValuePair<string, string seq> seq,
       cancellationToken: CancellationToken)
      : Task<Result<unit, PerlaMdResult>> =
      taskResult {
        do!
          ShouldRunFor command.Name [
            "build"
            "test"
            "serve"
            "templates"
            "new"
          ]

        do! env.IsAlreadySetUp() |> Result.requireFalse Continue

        Logger.log "Looks like this is your first time using perla..."
        Logger.log "Setting up perla for the first time..."

        let isInCI = HasDirective CliDirectives.CiRun directives

        do!
          env.RunSetup(isInCI, cancellationToken)
          |> TaskResult.ofTask
          |> TaskResult.bind (fun result ->
            Task.FromResult(if result = 0 then Ok() else Error(Exit result)))
          |> TaskResult.ignore

        do!
          env.SaveSetup()
          |> Result.requireNotNull Continue
          |> Result.ignore
          |> Result.teeError (fun _ ->
            Logger.log
              "We failed to save some checks, while it shouldn't be a problem for your applications you should consider reporting this issue.")

        return ()
      }
      |> TaskResult.teeError (fun error ->
        match error with
        | Exit _ ->
          Logger.log "We were unable to set you up for the first time..."

          Logger.log
            "You can try again or report this issue to the perla team."
        | _ -> ())


    let esbuildBinCheck
      (env: #Esbuild)
      (command: Command,
       _: KeyValuePair<string, string seq> seq,
       cancellationToken: CancellationToken)
      : Task<Result<unit, PerlaMdResult>> =
      taskResult {
        do! ShouldRunFor command.Name [ "build"; "serve"; "test" ]

        if env.IsEsbuildPresent env.EsbuildVersion then
          return ()
        else
          try
            do! env.RunSetupEsbuild(env.EsbuildVersion, cancellationToken)
          with ex ->
            Logger.log (
              "We were not able to setup [bold red]esbuild[/]...",
              ex = ex,
              escape = false
            )

            return! Error(Exit 1)

          do!
            env.SaveEsbuildPresent env.EsbuildVersion
            |> Result.requireNotNull Continue
            |> Result.ignore
            |> Result.teeError (fun _ ->
              Logger.log
                "We failed to save some checks, while it shouldn't be a problem for your applications you should consider reporting this issue.")

          return ()
      }

    let templatesCheck
      (env: #Templates)
      (command: Command,
       _: KeyValuePair<string, string seq> seq,
       cancellationToken: CancellationToken)
      : Task<Result<unit, PerlaMdResult>> =
      taskResult {

        do! ShouldRunFor command.Name [ "new"; "templates" ]

        // if templates are present this will be true and short circuit
        do!
          env.TemplatesArePresent()
          |> Result.requireFalse Continue
          |> Result.ignore

        Logger.log
          "Looks like you don't have the default perla templates installed..."

        Logger.log "Installing default perla templates..."

        do!
          env.RunSetupTemplates(
            {
              fullRepositoryName =
                Some $"{Default_Templates_Repository}:{Default_Templates_Repository_Branch}"
              operation = RunTemplateOperation.Add
            },
            cancellationToken
          )
          |> Task.bind (fun result ->
            Task.FromResult(if result = 0 then Ok() else Error(Exit result)))
          |> TaskResult.ignore

        Logger.log "Successfully installed default perla templates..."

        do!
          env.SaveTemplatesArePresent()
          |> Result.requireNotNull Continue
          |> Result.ignore
          |> Result.teeError (fun _ ->
            Logger.log
              "Failed to install templates, it is not possible continue..."

            Logger.log
              "Please try again, if this keeps happening please report this issue.")

        return ()
      }

    let fableCheck
      (env: #Fable)
      (command: Command,
       _: KeyValuePair<string, string seq> seq,
       cancellationToken: CancellationToken)
      : Task<Result<unit, PerlaMdResult>> =
      taskResult {
        do! ShouldRunFor command.Name [ "build"; "serve"; "test" ]

        if not env.IsFableInConfig then
          return ()
        else
          do!
            env.IsFablePresent cancellationToken
            // if Fable is already set this will be true and will shortcut
            |> TaskResult.requireFalse Continue
            |> TaskResult.ignore

          Logger.log
            "Looks like you don't have fable installed, we'll try to call [yellow]dotnet tool restore[/]."

          do!
            env.RestoreFable(cancellationToken)
            |> TaskResult.teeError (fun err ->
              Logger.log "dotnet tool restore failed:"
              Logger.log err
              Logger.log "Please try installing fable manually and try again.")
            |> TaskResult.mapError (fun _ -> Exit 1)

          return ()
      }

    let runDotEnv
      (env: #DotEnv)
      (command: Command, _: KeyValuePair<string, string seq> seq)
      : Task<Result<unit, PerlaMdResult>> =
      taskResult {
        do! ShouldRunFor command.Name [ "build"; "serve"; "test" ]

        env.GetDotEnvFiles() |> env.LoadDotEnvFiles

        return ()
      }

  let PreviewCheck =
    MiddlewareImpl.previewCheck
    |> PerlaCliMiddleware.AsMiddleware
    |> MiddlewareImpl.ToSCLMiddleware

  let EsbuildPluginCheck (env: #CliMiddlewareEnv) =
    MiddlewareImpl.esbuildPluginCheck env.Esbuild
    |> PerlaCliMiddleware.AsMiddleware
    |> MiddlewareImpl.ToSCLMiddleware

  let SetupCheck (env: #CliMiddlewareEnv) =

    MiddlewareImpl.setupCheck env.Setup
    |> PerlaCliMiddleware.AsCancellableMiddleware
    |> MiddlewareImpl.ToSCLMiddleware

  let EsbuildBinCheck (env: #CliMiddlewareEnv) =
    MiddlewareImpl.esbuildBinCheck env.Esbuild
    |> PerlaCliMiddleware.AsCancellableMiddleware
    |> MiddlewareImpl.ToSCLMiddleware


  let TemplatesCheck (env: #CliMiddlewareEnv) =
    MiddlewareImpl.templatesCheck env.Templates
    |> PerlaCliMiddleware.AsCancellableMiddleware
    |> MiddlewareImpl.ToSCLMiddleware

  let FableCheck (env: #CliMiddlewareEnv) =
    MiddlewareImpl.fableCheck env.Fable
    |> PerlaCliMiddleware.AsCancellableMiddleware
    |> MiddlewareImpl.ToSCLMiddleware

  let RunDotEnv (env: #CliMiddlewareEnv) =
    MiddlewareImpl.runDotEnv env.DotEnv
    |> PerlaCliMiddleware.AsMiddleware
    |> MiddlewareImpl.ToSCLMiddleware
