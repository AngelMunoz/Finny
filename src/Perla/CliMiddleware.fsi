namespace Perla.CliMiddleware


open System
open System.Threading.Tasks
open System.CommandLine.Invocation
open LiteDB

open FSharp.UMX

open Perla.Units
open Perla.Types

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
  val previewCheck: MiddlewareFn
  val esbuildPluginCheck: PerlaConfig -> MiddlewareFn

  val setupCheck: checks: SetupChecks -> MiddlewareFn

  val esbuildBinCheck: checks: EsbuildBinCheck -> MiddlewareFn

  val templatesCheck:
    templatesArePresent: (unit -> bool) * setCheck: (unit -> ObjectId) ->
      MiddlewareFn

  val fableCheck: isFableInConfig: bool -> MiddlewareFn

  val runDotEnv: MiddlewareFn

module Middleware =
  val PreviewCheck: InvocationMiddleware
  val EsbuildPluginCheck: InvocationMiddleware
  val SetupCheck: InvocationMiddleware
  val EsbuildBinCheck: InvocationMiddleware
  val TemplatesCheck: InvocationMiddleware
  val FableCheck: InvocationMiddleware
  val RunDotEnv: InvocationMiddleware
