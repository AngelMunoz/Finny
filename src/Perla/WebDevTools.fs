namespace Perla.WebDevTools

open System.Net.Mime
open System.Text

open System.Threading.Tasks
open Microsoft.AspNetCore.Http

open Falco.Markup
open Spectre.Console


type WebTemplates =
  static member Layout
    (
      content: XmlNode list,
      ?header: XmlNode list,
      ?footer: XmlNode list,
      ?head: XmlNode list
    ) =
    let header = defaultArg header []
    let footer = defaultArg footer []

    Elem.html [ Attr.lang "en" ] [
      Elem.head [] [
        Elem.title [] [ Text.raw "Perla Dev Tools" ]
        Elem.script [
          Attr.src "https://unpkg.com/htmx.org@1.9.2"
          Attr.integrity
            "sha384-L6OqL9pRWyyFU3+/bjdSri+iIphTN/bvYyM37tICVyOJkWZLpP2vGn6VUEXgzg6h"
          Attr.crossorigin "anonymous"
        ] []
        match head with
        | Some headNodes -> yield! headNodes
        | None -> ()
      ]
      Elem.body [] [
        Elem.article [] [
          Elem.header [] header
          Elem.main [] content
          Elem.footer [] footer
        ]
      ]
    ]

  static member Layout
    (
      header: XmlNode list,
      content: XmlNode list,
      ?footer: XmlNode list
    ) =
    WebTemplates.Layout(content = content, header = header, ?footer = footer)

[<RequireQualifiedAccess>]
module WebDevTools =
  let renderHtml (content: XmlNode) =
    Results.Text(renderHtml content, MediaTypeNames.Text.Html, Encoding.UTF8)

  let Index (ctx: HttpContext) : Task<IResult> =
    WebTemplates.Layout(
      [ Elem.h1 [] [ Text.enc "Perla Dev Tools" ] ],
      [ Elem.p [] [ Text.enc "Hopefully this one will be worth the effort!" ] ]
    )
    |> renderHtml
    |> Task.FromResult
