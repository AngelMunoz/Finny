module Main


open Sutil
open Sutil.DOM
open Fable.Core.JsInterop

importSideEffects "./styles.css"
importSideEffects "./second.css"

// Start the app
App.view () |> Program.mountElement "fable-app"
