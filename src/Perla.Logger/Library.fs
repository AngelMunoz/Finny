namespace Perla.Logger

open System.Threading.Tasks
open Spectre.Console
open System
open Microsoft.Extensions.Logging

[<Struct>]
type PrefixKind =
  | Log
  | Scaffold
  | Build
  | Serve

[<Struct>]
type LogEnding =
  | NewLine
  | SameLine

[<RequireQualifiedAccess>]
module Constants =

  [<Literal>]
  let LogPrefix = "Perla:"

  [<Literal>]
  let ScaffoldPrefix = "Scaffolding:"

  [<Literal>]
  let BuildPrefix = "Build:"

  [<Literal>]
  let ServePrefix = "Serve:"

type Logger =
  static member private format
    (prefix: PrefixKind list)
    (message: string)
    : FormattableString =
    let prefix =
      prefix
      |> List.fold
           (fun cur next ->
             let pr =
               match next with
               | PrefixKind.Log -> Constants.LogPrefix
               | PrefixKind.Scaffold -> Constants.ScaffoldPrefix
               | PrefixKind.Build -> Constants.BuildPrefix
               | PrefixKind.Serve -> Constants.ServePrefix

             $"{cur}{pr}")
           ""

    $"[yellow]{prefix}[/] {message}"

  static member log(message, ?ex: exn, ?prefixes, ?ending, ?escape) =
    let prefixes =
      let prefixes = defaultArg prefixes [ Log ]

      if prefixes.Length = 0 then [ Log ] else prefixes

    let escape = defaultArg escape true
    let formatted = Logger.format prefixes message

    match (defaultArg ending NewLine) with
    | NewLine ->
      if escape then
        AnsiConsole.MarkupLineInterpolated formatted
      else
        AnsiConsole.MarkupLine(formatted.ToString())
    | SameLine ->
      if escape then
        AnsiConsole.MarkupInterpolated formatted
      else
        AnsiConsole.Markup(formatted.ToString())

    match ex with
    | Some ex ->
#if DEBUG
      AnsiConsole.WriteException(
        ex,
        ExceptionFormats.ShortenEverything ||| ExceptionFormats.ShowLinks
      )
#else
      AnsiConsole.WriteException(
        ex,
        ExceptionFormats.ShortenPaths ||| ExceptionFormats.ShowLinks
      )
#endif
    | None -> ()

  static member scaffold(message, ?ex: exn, ?ending, ?escape) =
    Logger.log (
      message,
      ?ex = ex,
      prefixes = [ Log; Scaffold ],
      ?ending = ending,
      ?escape = escape
    )

  static member build(message, ?ex: exn, ?ending, ?escape) =
    Logger.log (
      message,
      ?ex = ex,
      prefixes = [ Log; Build ],
      ?ending = ending,
      ?escape = escape
    )

  static member serve(message, ?ex: exn, ?ending, ?escape) =
    Logger.log (
      message,
      ?ex = ex,
      prefixes = [ Log; Serve ],
      ?ending = ending,
      ?escape = escape
    )

  static member spinner<'Operation>
    (
      title: string,
      task: Task<'Operation>
    ) : Task<'Operation> =
    let status = AnsiConsole.Status()
    status.Spinner <- Spinner.Known.Dots
    status.StartAsync(title, (fun _ -> task))

  static member spinner<'Operation>
    (
      title: string,
      task: Async<'Operation>
    ) : Task<'Operation> =
    let status = AnsiConsole.Status()
    status.Spinner <- Spinner.Known.Dots
    status.StartAsync(title, (fun _ -> task |> Async.StartAsTask))


  static member inline spinner<'Operation>
    (
      title: string,
      [<InlineIfLambda>] operation: StatusContext -> Task<'Operation>
    ) : Task<'Operation> =
    let status = AnsiConsole.Status()
    status.Spinner <- Spinner.Known.Dots
    status.StartAsync(title, operation)

  static member inline spinner<'Operation>
    (
      title: string,
      [<InlineIfLambda>] operation: StatusContext -> Async<'Operation>
    ) : Task<'Operation> =
    let status = AnsiConsole.Status()
    status.Spinner <- Spinner.Known.Dots
    status.StartAsync(title, (fun ctx -> operation ctx |> Async.StartAsTask))

module Logger =

  let getPerlaLogger () =
    { new ILogger with
        member _.Log(logLevel, eventId, state, ex, formatter) =
          let format = formatter.Invoke(state, ex)
          Logger.log (format)

        member _.IsEnabled(level) = true

        member _.BeginScope(state) =
          { new IDisposable with
              member _.Dispose() = () } }
