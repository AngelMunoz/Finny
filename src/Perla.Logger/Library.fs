namespace Perla.Logger

open System.Threading.Tasks
open Spectre.Console
open System
open Microsoft.Extensions.Logging
open System.Runtime.InteropServices

[<Struct>]
type PrefixKind =
  | Log
  | Scaffold
  | Build
  | Serve
  | Esbuild

[<Struct>]
type LogEnding =
  | NewLine
  | SameLine

[<RequireQualifiedAccess>]
module Constants =

  [<Literal>]
  let LogPrefix = "Perla:"

  [<Literal>]
  let EsbuildPrefix = "Esbuild:"

  [<Literal>]
  let ScaffoldPrefix = "Scaffolding:"

  [<Literal>]
  let BuildPrefix = "Build:"

  [<Literal>]
  let ServePrefix = "Serve:"

type PrefixKind with

  member this.AsString =
    match this with
    | PrefixKind.Log -> Constants.LogPrefix
    | PrefixKind.Scaffold -> Constants.ScaffoldPrefix
    | PrefixKind.Build -> Constants.BuildPrefix
    | PrefixKind.Serve -> Constants.ServePrefix
    | PrefixKind.Esbuild -> Constants.EsbuildPrefix

module Internals =
  let format (prefix: PrefixKind list) (message: string) : FormattableString =
    let prefix =
      prefix |> List.fold (fun cur next -> $"{cur}{next.AsString}") ""

    $"[yellow]{prefix}[/] {message}"

type Logger =

  static member logCustom
    (
      message,
      [<Optional>] ?ex: exn,
      [<Optional>] ?prefixes: PrefixKind list,
      [<Optional>] ?ending: LogEnding,
      [<Optional>] ?escape: bool
    ) =
    let prefixes =
      let prefixes = defaultArg prefixes [ Log ]

      if prefixes.Length = 0 then [ Log ] else prefixes

    let escape = defaultArg escape true
    let formatted = Internals.format prefixes message

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

  static member log
    (
      message,
      [<Optional>] ?ex: exn,
      [<Optional>] ?target: PrefixKind,
      [<Optional>] ?escape: bool
    ) =
    let target =
      defaultArg target Log
      |> function
        | PrefixKind.Log -> [ Log ]
        | PrefixKind.Scaffold -> [ Log; Scaffold ]
        | PrefixKind.Build -> [ Log; Build ]
        | PrefixKind.Serve -> [ Log; Serve ]
        | PrefixKind.Esbuild -> [ Log; Esbuild ]

    Logger.logCustom (message, prefixes = target, ?ex = ex, ?escape = escape)

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
      [<InlineIfLambda>] operation: StatusContext -> Task<'Operation>,
      ?target: PrefixKind
    ) : Task<'Operation> =
    let prefix =
      defaultArg target Log
      |> function
        | PrefixKind.Log -> [ Log ]
        | PrefixKind.Scaffold -> [ Log; Scaffold ]
        | PrefixKind.Build -> [ Log; Build ]
        | PrefixKind.Serve -> [ Log; Serve ]
        | PrefixKind.Esbuild -> [ Log; Esbuild ]

    let title = Internals.format prefix title
    let status = AnsiConsole.Status()
    status.Spinner <- Spinner.Known.Dots
    status.StartAsync(title.ToString(), operation)

  static member inline spinner<'Operation>
    (
      title: string,
      [<InlineIfLambda>] operation: StatusContext -> Async<'Operation>,
      ?target: PrefixKind
    ) : Task<'Operation> =
    let prefix =
      defaultArg target Log
      |> function
        | PrefixKind.Log -> [ Log ]
        | PrefixKind.Scaffold -> [ Log; Scaffold ]
        | PrefixKind.Build -> [ Log; Build ]
        | PrefixKind.Serve -> [ Log; Serve ]
        | PrefixKind.Esbuild -> [ Log; Esbuild ]

    let title = Internals.format prefix title
    let status = AnsiConsole.Status()
    status.Spinner <- Spinner.Known.Dots

    status.StartAsync(
      title.ToString(),
      (fun ctx -> operation ctx |> Async.StartAsTask)
    )

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
