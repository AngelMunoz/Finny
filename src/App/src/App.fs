module App

open Sutil
open Sutil.DOM
open Sutil.Attr
open Fable.Core.JsInterop
open Sutil.Styling

open type Feliz.length

let Hello (name: string) : string = importMember "../public/ext.js"

let view () =
  let store = Store.make true

  Html.app [
    Html.main [
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
