module Main


open Sutil
open Fable.Core.JsInterop

let registerAll: unit -> unit = importMember "fsharp-components"


importSideEffects "./lit.js"
importSideEffects "./styles.css"
importSideEffects "../assets/fonts/fira_code.css"

registerAll ()
// Start the app
Program.mount ("fable-app", App.view ()) |> ignore
