# Perla Dev Server

[esbuild]: https://esbuild.github.io/
[import maps]: https://github.com/WICG/import-maps
[fable compiler]: https://fable.io/
[saturn]: https://saturnframework.org/
[skypack cdn]: https://www.skypack.dev/
[jspm cdn]: https://jspm.org/docs/cdn

> Check the sample https://github.com/AngelMunoz/perla-sample

> **Not using .NET?** check the releases tab for a binary distribution, no need to install anything, just get the binary on your path and run it.

This is an experimental project that aims to replace common and almost obligated nodejs tooling for greenfield projects using some state of the art technologies like

- [import maps]
- [skypack cdn]
- [jspm cdn]

And battle tested technologies like

- [esbuild]
- [fable compiler]
- [saturn]

To allow frontend development without having to install any kind of dependency on your local machine, with the help of import maps we are able to import dependencies directly from the skypack cdn. This apart from removing the need for a local node environment and tools like npm <sup>\*</sup> also improves security against npm compromised packages, since imports are handled via cdn the code is executed in the browser's sandbox.

### Downsides

This tool assumes you're using standard Html, CSS and Javascript (Fable for F# users), currently there's no plan to support transpilers other than Fable and even then it might need to be refactored out to another tool if needed. That means if you want to use things like typescript/sass/less/pug you would need to pre-compile things first to then allow esbuild and the dev server do the rest.

## Status

THis project is in its early stages of development, current goals at this point are:

- [x] For .NET users, remove npm/node out of the equation.
- [x] For F# users, seamless fable integration.
- [x] For .NET users, have a development server, HMR/auto-reload are not yet in the works.
- [x] For .NET users, Build for production using esbuild.
- [x] For .NET users, provide a cli dotnet tool
- [x] Binary Release for users outside .NET
- [x] Autoreload on change

### Future Goals

Including the previous goals the future goals include

- [ ] Typescript (ts/tsx) and jsx support
- [ ] HMR
- [ ] Plugin System

### Quick Command View
```
USAGE: perla [--help] [--version] [<subcommand> [<options>]]

SUBCOMMANDS:

    serve, s <options>    Starts a development server for modern Javascript development
    build, b <options>    Builds the specified JS and CSS resources for production
    init <options>        Creates basic files and directories to start using fds.
    search, se <options>  Searches a package in the skypack API.
    show <options>        Gets the skypack information about a package.
    add <options>         Generates an entry in the import map.
    remove <options>      Removes an entry in the import map.

    Use 'perla <subcommand> --help' for additional information.

OPTIONS:

    --version, -v         Prints out the cli version to the console.
    --help                display this list of options.
```

> <sup>\*</sup>: The first releases are aimed at the .NET community via _dotnet tools_ but, if this project turns out to be useful beyond that we will enable binary distributions which should be executable for any environment


## Existing tools

If you actually use and like nodejs, then you would be better taking a look at the tools that inspired this repository

- [snowpack](https://www.snowpack.dev/)
- [vite](https://vitejs.dev/)

These tools have a bigger community and rely on an even bigger ecosystem plus they support plugins via npm so if you're using node stick with them they are a better choice
Perla's unbundled development was inspired by both snowpack and vite, CDN dependencies were inspired by snowpack's remote sources development



