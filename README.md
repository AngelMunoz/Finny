# FSharp.DevServer

[esbuild]: https://esbuild.github.io/
[import maps]: https://github.com/WICG/import-maps
[fable compiler]: https://fable.io/
[saturn]: https://saturnframework.org/
[skypack cdn]: https://www.skypack.dev/

This is an experimental project that aims to replace common and almost obligated nodejs tooling for greenfield projects using some state of the art technologies like

- [import maps]
- [skypack cdn]

And battle tested technologies like

- [esbuild]
- [fable compiler]
- [saturn]

To allow frontend development without having to install any kind of dependency on your local machine, with the help of import maps we are able to import dependencies directly from the skypack cdn. This apart from removing the need for a local node environment and tools like npm <sup>\*</sup> also improves security against npm compromised packages, since imports are handled via cdn the code is executed in the browser's sandbox.

### Downsides

This tool assumes you're using standard Html, CSS and Javascript (Fable for F# users), currently there's no plan to support transpilers other than Fable and even then it might need to be refactored out to another tool if needed. That means if you want to use things like typescript/sass/less/pug you would need to pre-compile things first to then allow esbuild and the dev server do the rest.

## Status

THis project is in its early stages of development, current goals at this point are:

- [ ] For .NET users, remove npm/node out of the equation.
- [ ] For F# users, seamless fable integration.
- [ ] For .NET users, have a development server, HMR/auto-reload are not yet in the works.
- [ ] For .NET users, Build for production using esbuild.
- [ ] For .NET users, provide a cli dotnet tool

### Future Goals

Including the previous goals the future goals include

- [ ] Autoreload on change
- [ ] HMR
- [ ] Binary Release for users outside .NET

current commands are

```
USAGE: FSharp.DevServer.exe [--help] [--version] [--fable-auto-start [<bool>]] [--fable-project [<string>]] [--fable-extension [<string>]] [--fable-out-dir [<string>]] [<subcommand> [<options>]]

SUBCOMMANDS:

    server, s <options>   Starts a development server for modern Javascript development
    build, b <options>    Builds the specified JS and CSS resources for production
    init <options>        Creates basic files and directories to start using fds.
    search, se <options>  Searches a package in the skypack API.
    show <options>        Gets the skypack information about a package.
    add <options>         Generates an entry in the import map.
    remove <options>      Removes an entry in the import map.

    Use 'FSharp.DevServer.exe <subcommand> --help' for additional information.

OPTIONS:

    --version, -v         Prints out the cli version to the console.
    --fable-auto-start, -fa [<bool>]
                          Auto-start fable in watch mode. Defaults to true, overrides the config file
    --fable-project, -fp [<string>]
                          The fsproject to use with fable. Defaults to "./src/App.fsproj", overrides the config file
    --fable-extension, -fe [<string>]
                          The extension to use with fable output files. Defaults to ".fs.js", overrides the config file
    --fable-out-dir, -fo [<string>]
                          Where to output the fable compiled files. Defaults to "./public", overrides the config file
    --help                display this list of options.
```

> <sup>\*</sup>: The first releases are aimed at the .NET community via _dotnet tools_ but, if this project turns out to be useful beyond that we will enable binary distributions which should be executable for any environment
