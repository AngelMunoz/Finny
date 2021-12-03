module Perla.Plugins

open System
open System.IO
open FSharp.Compiler.SourceCodeServices
open FSharp.Compiler.Interactive.Shell
open Types

module ScriptedContent =

  let getContentFromScripts (file: FileInfo) : string * string =
    let defConfig = FsiEvaluationSession.GetDefaultConfiguration()

    let argv =
      [| "fsi.exe"
         "--noninteractive"
         "--nologo"
         "--gui-" |]

    use stdIn = new StringReader("")
    use stdOut = new StringWriter()
    use stdErr = new StringWriter()

    use session =
      FsiEvaluationSession.Create(defConfig, argv, stdIn, stdOut, stdErr, true)


    let interaction =
      use script = file.OpenText()
      session.EvalInteractionNonThrowing(script.ReadToEnd())


    match interaction with
    | Choice2Of2 ex, error -> raise (ex)
    | Choice1Of2 None, error -> failwith $"Unsuported type %A{error}"
    | Choice1Of2 (Some value), error ->
      match value.ReflectionValue with
      | :? (list<list<string>>) as migrations ->
        let up =
          (List.tryHead migrations |> Option.defaultValue [])
          |> List.reduce (fun next curr -> $"{next}\n{curr}")

        let down =
          (List.tryLast migrations |> Option.defaultValue [])
          |> List.reduce (fun next curr -> $"{next}\n{curr}")

        $"\n{up}", $"\n{down}"
      | _ -> failwith $"Unsuported type %A{value.ReflectionType}"
