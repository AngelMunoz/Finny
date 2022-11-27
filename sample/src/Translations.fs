module Translations

open System
open Fable.Core
open Types
open Sutil

let private translations = JsInterop.importDefault "./translations.json?module"

let fetchTranslations () : TranslationCollection = translations

let matchTranslationLanguage
  (
    translations: TranslationCollection option,
    language: Language
  )
  =
  match translations, language with
  | Some translations, EnUs -> translations["en-us"]
  | Some translations, DeDe -> translations["de-de"]
  | _, _ -> None

let getTranslationValue
  (translationKey: string)
  (language: TranslationMap option)
  =
  language |> Option.map (fun trMap -> trMap[translationKey]) |> Option.flatten

let T
  (store: IObservable<TranslationCollection option * Language>)
  (key: string, defValue: string)
  =
  Observable.map
    (matchTranslationLanguage
     >> (getTranslationValue key)
     >> Option.defaultValue defValue)
    store
