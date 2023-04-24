namespace Perla.CliMiddleware


open System.Collections.Generic
open System.CommandLine
open System.CommandLine.Invocation
open System.Threading
open System.Threading.Tasks

open LiteDB

open FSharp.UMX

open Perla.Units
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
    val ShouldRunFor:
      candidate: string -> commands: string list -> Result<unit, PerlaMdResult>

    val HasDirective:
      directive: string ->
      directives: KeyValuePair<string, string seq> seq ->
        bool

    val ToSCLMiddleware: middleware: PerlaCliMiddleware -> InvocationMiddleware

    val previewCheck:
      command: Command * directives: KeyValuePair<string, string seq> seq ->
        Task<Result<unit, PerlaMdResult>>

    val esbuildPluginCheck:
      env: #Esbuild ->
      command: Command * directives: KeyValuePair<string, string seq> seq ->
        Task<Result<unit, PerlaMdResult>>

    val setupCheck:
      env: #Setup ->
      command: Command *
      directives: KeyValuePair<string, string seq> seq *
      CancellationToken ->
        Task<Result<unit, PerlaMdResult>>

    val esbuildBinCheck:
      env: #Esbuild ->
      command: Command *
      directives: KeyValuePair<string, string seq> seq *
      CancellationToken ->
        Task<Result<unit, PerlaMdResult>>

    val templatesCheck:
      env: #Templates ->
      command: Command *
      directives: KeyValuePair<string, string seq> seq *
      CancellationToken ->
        Task<Result<unit, PerlaMdResult>>

    val fableCheck:
      env: #Fable ->
      command: Command *
      directives: KeyValuePair<string, string seq> seq *
      CancellationToken ->
        Task<Result<unit, PerlaMdResult>>

    val runDotEnv:
      env: #DotEnv ->
      command: Command * directives: KeyValuePair<string, string seq> seq ->
        Task<Result<unit, PerlaMdResult>>

  val PreviewCheck: InvocationMiddleware
  val EsbuildPluginCheck: env: #CliMiddlewareEnv -> InvocationMiddleware
  val SetupCheck: env: #CliMiddlewareEnv -> InvocationMiddleware
  val EsbuildBinCheck: env: #CliMiddlewareEnv -> InvocationMiddleware
  val TemplatesCheck: env: #CliMiddlewareEnv -> InvocationMiddleware
  val FableCheck: env: #CliMiddlewareEnv -> InvocationMiddleware
  val RunDotEnv: env: #CliMiddlewareEnv -> InvocationMiddleware
