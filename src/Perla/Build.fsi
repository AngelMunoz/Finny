namespace Perla.Build

open System
open System.IO
open System.Net.Http
open System.Threading.Tasks
open AngleSharp
open AngleSharp.Html.Parser
open Perla
open Perla.Types
open Perla.Units
open Perla.Json
open Perla.FileSystem
open Perla.VirtualFs
open Perla.Logger
open Perla.Esbuild
open Fake.IO.Globbing
open FSharp.UMX


[<Class>]
type Build =
    static member Execute: config: PerlaConfig -> Task<unit>
