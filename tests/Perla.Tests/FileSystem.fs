module Perla.Tests.FileSystem

open Xunit
open System.IO

open Perla
open Perla.FileSystem
open FSharp.UMX
open System.Diagnostics

[<Fact>]
let ``Database Should contain AssemblyRoot`` () =
  let database = UMX.untag FileSystem.Database
  Assert.Contains(UMX.untag FileSystem.AssemblyRoot, database)

[<Fact>]
let ``Templates Should contain AssemblyRoot`` () =
  let templates = UMX.untag FileSystem.Templates
  Assert.Contains(UMX.untag FileSystem.AssemblyRoot, templates)
