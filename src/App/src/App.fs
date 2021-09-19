module App

open Sutil
open Sutil.DOM
open Sutil.Attr
open Fable.Core.JsInterop
open Sutil.Styling

open type Feliz.length

let registerAll: unit -> unit = importMember "fsharp-components"

registerAll ()

let Hello (name: string) : string = importMember "../public/ext.js"

let view () =
  let store = Store.make true

  Html.app [
    Html.main [
      Html.custom (
        "fs-message",
        [ Attr.custom ("is-open", "")
          Attr.custom ("header", "I Can't Believe it's working!")
          Attr.custom ("kind", "info")
          Html.p "This is WORKING" ]
      )
      Html.label [
        Html.input [
          type' "checkbox"
          Bind.attr ("checked", store)
        ]
        text "Show Text"
      ]
      Html.p [
        Bind.attr ("hidden", (store .> not))
        text "Hey there! this is some Sutil + Fable stuff!"
        text $"""Hello {Hello("F#")}!"""
      ]
    ]
  ]
  |> withStyle [
       rule
         "label"
         [ Css.fontSize (em 1.5)
           Css.color "rebeccapurple" ]
     ]
