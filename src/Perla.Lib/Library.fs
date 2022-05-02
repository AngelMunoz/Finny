namespace Perla.Lib

open System.Text
open Perla.Lib.Types

[<AutoOpen>]
module Lib =

  let (|ParseRegex|_|) regex str =
    let m = RegularExpressions.Regex(regex).Match(str)

    if m.Success then
      Some(List.tail [ for x in m.Groups -> x.Value ])
    else
      None

  let parseUrl url =
    match url with
    | ParseRegex @"https://cdn.skypack.dev/pin/(@?[^@]+)@v([\d.]+)"
                 [ name; version ] -> Some(Source.Skypack, name, version)
    | ParseRegex @"https://cdn.jsdelivr.net/npm/(@?[^@]+)@([\d.]+)"
                 [ name; version ] -> Some(Source.Jsdelivr, name, version)
    | ParseRegex @"https://ga.jspm.io/npm:(@?[^@]+)@([\d.]+)" [ name; version ] ->
      Some(Source.Jspm, name, version)
    | ParseRegex @"https://unpkg.com/(@?[^@]+)@([\d.]+)" [ name; version ] ->
      Some(Source.Unpkg, name, version)
    | _ -> None
