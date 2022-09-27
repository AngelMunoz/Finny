namespace Perla.Lib

open System
open System.Collections.Concurrent
open System.IO
open FSharp.Compiler.Interactive.Shell
open Perla.Lib.Plugin
open Spectre.Console

module internal Fsi =
  let GetPluginCache () =
    ConcurrentDictionary<PluginInfo, FsiEvaluationSession>()

  type Fsi =

    static member getSession(?argv, ?stdout, ?stderr) =
      let defaultArgv =
        [| "fsi.exe"; "--optinize+"; "--nologo"; "--gui-"; "--readline-" |]

      let argv =
        match argv with
        | Some argv -> [| yield! defaultArgv; yield! argv |]
        | None -> defaultArgv

      let stdout = defaultArg stdout Console.Out
      let stderr = defaultArg stderr Console.Error

      let config = FsiEvaluationSession.GetDefaultConfiguration()

      FsiEvaluationSession.Create(
        config,
        argv,
        new StringReader(""),
        stdout,
        stderr,
        true
      )
