namespace Perla.Tests

open Xunit
open System.IO

open Perla
open Perla.FileSystem
open FSharp.UMX
open System

type FileSystem()=

  do FileSystem.SetCwdToPerlaRoot() 

  [<Fact>]
  member _.``Database Should contain AssemblyRoot`` () =
    let database = UMX.untag FileSystem.Database
    Assert.Contains(UMX.untag FileSystem.AssemblyRoot, database)

  [<Fact>]
  member _.``Templates Should contain AssemblyRoot`` () =
    let templates = UMX.untag FileSystem.Templates
    Assert.Contains(UMX.untag FileSystem.AssemblyRoot, templates)

  [<Fact>]
  member _.``GetConfigPath brings Perla perla.jsonc path correctly`` () =
    let expected = FileSystem.CurrentWorkingDirectory() |> UMX.untag
    let actual = FileSystem.GetConfigPath Constants.PerlaConfigName None |> UMX.untag
    Assert.Equal(expected, actual |> Path.GetDirectoryName)

  interface IDisposable with
    member this.Dispose(): unit = 
      Directory.SetCurrentDirectory (UMX.untag FileSystem.AssemblyRoot)
