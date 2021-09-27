module App

open Browser.Types
open Sutil
open Sutil.DOM
open Sutil.Attr
open Fable.Core.JsInterop
open Sutil.Styling

open type Feliz.length

let registerAll: unit -> unit = importMember "fsharp-components"
importSideEffects "./lit.js"
importSideEffects "./styles.css"

registerAll ()

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
      Bind.el (
        store,
        (fun isOpen ->
          if isOpen then
            Html.p [
              text "Hey there! this is some Sutil + Fable stuff!"
            ]
          else
            Html.none)
      )
      Bind.el (
        store,
        (fun isOpen ->
          if isOpen |> not then
            Html.custom (
              "fs-message",
              [ Attr.custom ("header", "I Can't Believe it's working!")
                Attr.custom ("kind", "info")
                Html.p "This is WORKING" ]
            )
          else
            Html.none)
      )
    ]
  ]
  |> withStyle [
       rule
         "label"
         [ Css.fontSize (em 1.5)
           Css.color "rebeccapurple" ]
     ]
