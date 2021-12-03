namespace Clam

open System
open System.Diagnostics
open System.IO

type PathExt() =

  static member ClamRootDirectory =
    let assemblyLoc =
      Path.GetDirectoryName(Reflection.Assembly.GetEntryAssembly().Location)

    if String.IsNullOrWhiteSpace assemblyLoc then
      Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)
    else
      assemblyLoc

  static member LocalDBPath =
    Path.Combine(PathExt.ClamRootDirectory, "templates.db")

  static member TemplatesDirectory =
    Path.Combine(PathExt.ClamRootDirectory, "templates")
