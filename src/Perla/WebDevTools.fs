namespace Perla.WebDevTools

open System.Net.Mime
open System.Text

open System.Threading.Tasks
open Microsoft.AspNetCore.Http

open Falco.Markup
open Spectre.Console


[<AutoOpen>]
module WebDevToolsExtensions =
  type HttpContext with

    member this.IsBoosted =
      match this.Request.Headers.TryGetValue "HX-Boosted" with
      | true, _ -> true
      | false, _ -> false

    member this.IsHtmx =
      match this.Request.Headers.TryGetValue "HX-Request" with
      | true, value -> value = "true"
      | false, _ -> false

[<Struct>]
type NavigationTab =
  | FileSystem
  | Configuration
  | Environment
  | Dependencies
  | Templating
  | Index

[<AutoOpen>]

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
        Elem.meta [ Attr.charset "utf-8" ]
        Elem.meta [
          Attr.name "viewport"
          Attr.content "width=device-width, initial-scale=1"
        ]
        Elem.title [] [ Text.raw "Perla Dev Tools" ]
        Elem.link [
          Attr.rel "stylesheet"
          Attr.href
            "https://cdn.jsdelivr.net/npm/uikit@3.16.15/dist/css/uikit.min.css"
        ]
        Elem.script [
          Attr.src "https://unpkg.com/htmx.org@1.9.2"
          Attr.integrity
            "sha384-L6OqL9pRWyyFU3+/bjdSri+iIphTN/bvYyM37tICVyOJkWZLpP2vGn6VUEXgzg6h"
          Attr.crossorigin "anonymous"
        ] []
        Elem.script [
          Attr.src
            "https://cdn.jsdelivr.net/npm/uikit@3.16.15/dist/js/uikit.min.js"
          Attr.integrity "sha256-Hzqxe+zKM6ropQVt5k6eDDIm6g/XIJeo2z6NsQN4Fu0="
          Attr.crossorigin "anonymous"
        ] []
        Elem.script [
          Attr.src
            "https://cdn.jsdelivr.net/npm/uikit@3.16.15/dist/js/uikit-icons.min.js"
          Attr.integrity "sha256-Aid4LgdC5UHKQ+Epbt0wz+r67SL5JqKDCKtwYe2ggw4="
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

  static member BoostedLayout
    (
      content: XmlNode list,
      header: XmlNode list,
      ?footer: XmlNode list
    ) =
    let footer = defaultArg footer []

    Elem.article [] [
      Elem.header [] header
      Elem.main [] content
      Elem.footer [] footer
    ]

  static member Body
    (
      content: XmlNode list,
      ?boosted: bool,
      ?activeTab: NavigationTab
    ) =
    let boosted = defaultArg boosted false
    let activeTab = defaultArg activeTab NavigationTab.Index

    let header = [
      Elem.nav [
        Attr.class' "uk-navbar-container"
        Attr.create "uk-navbar" ""
        Attr.create "hx-boost" "true"
      ] [
        Elem.div [ Attr.class' "uk-navbar-center" ] [
          Elem.ul [ Attr.class' "uk-navbar-nav" ] [

            Elem.li [
              if activeTab = NavigationTab.FileSystem then
                Attr.class' "uk-active"
            ] [
              Elem.a [ Attr.href "./file-system.html" ] [
                Text.enc "File System"
              ]
            ]
            Elem.li [
              if activeTab = NavigationTab.Configuration then
                Attr.class' "uk-active"
            ] [
              Elem.a [ Attr.href "./configuration.html" ] [
                Text.enc "Configuration"
              ]
            ]
            Elem.li [
              if activeTab = NavigationTab.Environment then
                Attr.class' "uk-active"
            ] [
              Elem.a [ Attr.href "./environment.html" ] [
                Text.enc "Environment"
              ]
            ]
            Elem.li [
              if activeTab = NavigationTab.Dependencies then
                Attr.class' "uk-active"
            ] [
              Elem.a [ Attr.href "./dependencies.html" ] [
                Text.enc "ImportMap & Dependencies"
              ]
            ]
            Elem.li [
              if activeTab = NavigationTab.Templating then
                Attr.class' "uk-active"
            ] [
              Elem.a [ Attr.href "./templating.html" ] [
                Text.enc "Project Templates"
              ]
            ]
          ]
        ]
      ]
    ]

    if boosted then
      WebTemplates.BoostedLayout(content, header)
    else
      WebTemplates.Layout(content, header)

[<RequireQualifiedAccess>]
module WebDevTools =
  let renderHtml (content: XmlNode) =
    Results.Text(renderHtml content, MediaTypeNames.Text.Html, Encoding.UTF8)

  let Index (ctx: HttpContext) : Task<IResult> =
    // Virtual File System Info
    // Current Configuration Info
    // Current Environment Info
    // On the Fly resource transpilation
    // Import Map Info and manipulation // dependencies
    // Scaffolding Template Info
    WebTemplates.Body(
      [
        Elem.h1 [] [ Text.enc "Perla Dev Tools" ]
        Elem.p [] [ Text.enc "Welcome to Perla Dev Tools" ]
      ],
      ctx.IsBoosted
    )
    |> renderHtml
    |> Task.FromResult

  let FileSystem (ctx: HttpContext) : Task<IResult> =
    WebTemplates.Body(
      [
        Elem.h1 [] [ Text.enc "Perla Dev Tools" ]
        Elem.p [] [ Text.enc "Welcome to Perla Dev Tools" ]
      ],
      ctx.IsBoosted,
      NavigationTab.FileSystem
    )
    |> renderHtml
    |> Task.FromResult

  let Configuration (ctx: HttpContext) : Task<IResult> =
    WebTemplates.Body(
      [
        Elem.h1 [] [ Text.enc "Perla Dev Tools" ]
        Elem.p [] [ Text.enc "Welcome to Perla Dev Tools" ]
      ],
      ctx.IsBoosted,
      NavigationTab.Configuration
    )
    |> renderHtml
    |> Task.FromResult

  let Environment (ctx: HttpContext) : Task<IResult> =
    WebTemplates.Body(
      [
        Elem.h1 [] [ Text.enc "Perla Dev Tools" ]
        Elem.p [] [ Text.enc "Welcome to Perla Dev Tools" ]
      ],
      ctx.IsBoosted,
      NavigationTab.Environment
    )
    |> renderHtml
    |> Task.FromResult

  let Dependencies (ctx: HttpContext) : Task<IResult> =
    WebTemplates.Body(
      [
        Elem.h1 [] [ Text.enc "Perla Dev Tools" ]
        Elem.p [] [ Text.enc "Welcome to Perla Dev Tools" ]
      ],
      ctx.IsBoosted,
      NavigationTab.Dependencies
    )
    |> renderHtml
    |> Task.FromResult

  let Templating (ctx: HttpContext) : Task<IResult> =
    WebTemplates.Body(
      [
        Elem.h1 [] [ Text.enc "Perla Dev Tools" ]
        Elem.p [] [ Text.enc "Welcome to Perla Dev Tools" ]
      ],
      ctx.IsBoosted,
      NavigationTab.Templating
    )
    |> renderHtml
    |> Task.FromResult
