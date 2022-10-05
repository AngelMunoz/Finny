namespace Perla

open Zio
open Zio.FileSystems

open Perla.Types
open Perla.Logger

/// <summary>
/// Encloses all of the operations related with operating with
/// source files for the user, reading files, mounting directories
/// resolving paths on disk, etc.
/// </summary>
/// <remarks>
/// At the moment this module doesn't handle any of the configuration files
/// already existing or handled by the <see cref="T:Perla.Lib.FS">Perla.Lib.Fs</see> module
/// </remarks>
module VirtualFS =

  let private currentFs = new PhysicalFileSystem()
  let private mounted = new MountFileSystem(new MemoryFileSystem(), true)
  let private perlaPaths = []

  let buildFileSystem projectRoot (config: PerlaConfig) =
    let mountedDirs =
      config.devServer
      |> Option.map (fun devServer -> devServer.mountDirectories)
      |> Option.flatten
      |> Option.defaultValue (Map.ofList [ "./src", "/src" ])

    ()

  let monitorSources () = ()

  let replaceAtDestination () = ()

  let getAtDestination () = ()
