module App

open Sutil
open Sutil.DOM
open Sutil.Attr
open Sutil.Styling

open type Feliz.length

type Language =
  | EnUs
  | DeDe
  | Spanish

  member this.AsString() =
    match this with
    | EnUs -> "en-US"
    | DeDe -> "de-DE"
    | Spanish -> "es-MX"

type TranslationValues =
  {| name: string option
     currentLang: string option |}

let translations: {| ``en-us``: TranslationValues
                     ``de-de``: TranslationValues |} =
  Fable.Core.JsInterop.importDefault "./translations.json?module"

let view () =
  let store = Store.make true
  let currentLang = Store.make (EnUs)

  let currentTranslations =
    currentLang
    .> (fun lang ->
      match lang with
      | EnUs -> translations.``en-us`` |> Some
      | DeDe -> translations.``de-de`` |> Some
      | Spanish -> None)

  let nameLabel =
    currentTranslations
    .> (fun tran ->
      tran
      |> Option.map (fun tran -> tran.name)
      |> Option.flatten
      |> Option.defaultValue "Primer Nombre ")

  let currentLangLabel =
    currentTranslations
    .> (fun tran ->
      tran
      |> Option.map (fun tran -> tran.currentLang)
      |> Option.flatten
      |> Option.defaultValue "Idioma Actual")

  Html.app [
    Html.main [

      Html.label [
        Html.input [
          type' "checkbox"
          Bind.attr ("checked", store)
        ]
        text "Show Text"
      ]
      Html.section [
        Html.div [
          Html.text "English"
          Html.input [
            type' "radio"
            Attr.name "language"
            Attr.isChecked true
            on "change" (fun _ -> currentLang <~ EnUs) []
          ]
        ]
        Html.div [
          Html.text "Deutsch"
          Html.input [
            type' "radio"
            Attr.name "language"
            on "change" (fun _ -> currentLang <~ DeDe) []
          ]
        ]
        Html.div [
          Html.text "Español"
          Html.input [
            type' "radio"
            Attr.name "language"
            on "change" (fun _ -> currentLang <~ Spanish) []
          ]
        ]
        Html.div [
          Bind.el (currentLangLabel, Html.text)
          Bind.el (currentLang, (fun lang -> Html.text $": {lang.AsString()}"))
        ]
        Bind.el (nameLabel, Html.text)
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
