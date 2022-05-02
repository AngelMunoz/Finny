[<AutoOpen>]
module Nacre.Extensions

open System
open System.Diagnostics
open System.IO

type Path with
    static member NacreExecPath: string =
        let assemblyLoc =
            Path.GetDirectoryName(Reflection.Assembly.GetEntryAssembly().Location)

        if String.IsNullOrWhiteSpace assemblyLoc then
            Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)
        else
            assemblyLoc
