module Components

open System
open Feliz
open Sutil
open Sutil.Styling
open Sutil.CoreElements

open type Feliz.length

open Types

let LanguageSelector
  (translations: IStore<TranslationCollection option * Language>)
  =
  let setLanguage language =
    Store.modify (fun (translations, _) -> translations, language)

  let setUS = setLanguage EnUs
  let setDE = setLanguage DeDe
  let setES = setLanguage EsMx

  let isUS =
    translations |> Observable.map (fun (_, language) -> language = EnUs)

  let isDE =
    translations |> Observable.map (fun (_, language) -> language = DeDe)

  let isES =
    translations |> Observable.map (fun (_, language) -> language = EsMx)

  Html.header [
    Html.section [
      Html.div [
        Html.input [
          type' "radio"
          Attr.id "language-en"
          Attr.name "language"
          Bind.attr ("checked", isUS)
          on "change" (fun _ -> setUS translations) []
        ]
        Html.label [ Html.text "English"; Attr.for' "language-en" ]
      ]
      Html.div [
        Html.input [
          type' "radio"
          Attr.id "language-de"
          Attr.name "language"
          Bind.attr ("checked", isDE)
          on "change" (fun _ -> setDE translations) []
        ]
        Html.label [ Html.text "Deutsch"; Attr.for' "language-de" ]
      ]
      Html.div [
        Html.input [
          type' "radio"
          Attr.id "language-es"
          Attr.name "language"
          Bind.attr ("checked", isES)
          on "change" (fun _ -> setES translations) []
        ]
        Html.label [ Html.text "Español"; Attr.for' "language-es" ]
      ]
    ]
  ]
  |> withStyle [
    rule "header" [ Css.displayFlex; Css.justifyContentFlexEnd ]
    rule "section" [
      Css.displayFlex
      Css.custom ("justify-content", "space-evenly")
    ]
  ]

let NotificationGenerator
  (Tr: string * string -> IObservable<string>)
  (notifications: IStore<Notification ResizeArray>)
  =
  let header = Store.make ""
  let content = Store.make ""
  let disableSubmit = Store.make false

  let canSubmitSub =
    Store.subscribe2 header content (fun (canH, canC) ->
      (String.IsNullOrWhiteSpace canH || String.IsNullOrWhiteSpace canC)
      |> Store.set disableSubmit)

  Html.form [
    disposeOnUnmount [ header; content; canSubmitSub ]
    Attr.className "nf"
    Ev.onSubmit (fun e ->
      e.preventDefault ()

      notifications.Value.Add(Notification.Create(header.Value, content.Value))

      Store.set notifications notifications.Value)
    Html.fieldSet [
      Html.label [
        Attr.for' "header-input"
        Bind.el (Tr("notification:header", "Encabezado"), Html.text)
      ]
      Html.input [
        Attr.className "nf-input"
        Attr.id "header-input"
        Ev.onTextInput (Store.set header)
      ]
      Html.label [
        Attr.for' "content-input"
        Bind.el (Tr("notification:message", "Contenido"), Html.text)
      ]
      Html.input [
        Attr.className "nf-input"
        Attr.id "content-input"
        Ev.onTextInput (Store.set content)
      ]
      Html.button [
        Attr.typeSubmit
        Attr.disabled disableSubmit
        Bind.el (
          Tr("notification:generate", "Generar Notificationes"),
          Html.text
        )
      ]
    ]
  ]
  |> withStyle [
    rule ".nf" [
      Css.displayFlex
      Css.flexDirectionColumn
      Css.margin length.auto
    ]
    rule ".nf fieldset" [
      Css.displayFlex
      Css.flexDirectionColumn
      Css.fontSize (em 1.5)
      Css.color "var(--primary-color)"
      // Css.custom("color", )
      Css.margin (em 0.5)
    ]
    rule "button" [ Css.marginTop (em 1) ]
  ]

let NotificationArea (notifications: IStore<Notification ResizeArray>) =
  let notifList = Observable.map (List.ofSeq) notifications

  Html.aside [
    Attr.className "notification-area"
    Bind.each (
      notifList,
      fun notif ->
        Html.custom (
          "fs-message",
          [
            Attr.custom ("header", notif.header)
            Attr.custom ("kind", notif.kind)
            Html.p notif.message
            onCustomEvent
              "fs-close-message"
              (fun _ ->
                notifications.Value.Remove(notif) |> ignore
                Store.set notifications notifications.Value)
              []
          ]
        )
    )
  ]
  |> withStyle [
    rule ".notification-area" [
      Css.positionAbsolute
      Css.top (perc (10))
      Css.right (perc (10))
    ]
  ]
