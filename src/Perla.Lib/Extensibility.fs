namespace Perla.Lib

open System.IO


module Extensibility =
  open FSharp.Compiler.Interactive.Shell

  let private getSession stdin stdout stderr =
    let defConfig = FsiEvaluationSession.GetDefaultConfiguration()

    let argv =
      [| "fsi.exe"
         "--noninteractive"
         "--nologo"
         "--gui-" |]

    FsiEvaluationSession.Create(defConfig, argv, stdin, stdout, stderr, true)

  let getConfigurationFromScript content =
    use stdin = new StringReader("")
    use stdout = new StringWriter()
    use stderr = new StringWriter()
    use session = getSession stdin stdout stderr

    session.EvalInteractionNonThrowing(content)
    |> ignore

    match session.TryFindBoundValue Constants.ScaffoldConfiguration with
    | Some bound -> Some bound.Value.ReflectionValue
    | None -> None
