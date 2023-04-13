# Perla Dev Server

[![Open in Gitpod](https://gitpod.io/button/open-in-gitpod.svg)](https://gitpod.io/#https://github.com/AngelMunoz/Perla)

Perla is a cross-platform single executable binary CLI Tool for a Development Server of Single Page Applications.

If that sounds like something nice, [Check The docs!](https://perla-docs.web.app/)

## Status

> take a peek of PoC's and new stuff that may (or not) be coming to perla in the experiments directory

This project is in development, current goals at this point are:

- [x] Remove npm/node out of the equation.
- [x] For F# users, seamless fable integration.
- [x] A Fast and easy to use Development server
- [x] Build for production using esbuild.
- [x] Binary Release for users outside .NET
- [ ] Plugin System
- [ ] Test runner for Client side tests powered by playwright
- [ ] Local Typescript Types (to help the IDE)

For more information check the Issues tab.

```
Description:
  The Perla Dev Server!

Usage:
  Perla [command] [options]

Options:
  --version       Show version information
  -?, -h, --help  Show help and usage information
  --info <info>   Brings the Help dialog []

Commands:
  setup                                  Initialized a given directory or perla itself
  t, templates <templateRepositoryName>  Handles Template Repository operations such as list, add, update, and remove templates
  describe, ds <properties>              Describes the perla.json file or it's properties as requested
  b, build                               Builds the SPA application for distribution
  s, serve, start                        Starts the development server and if fable projects are present it also takes care of it.
  test                                   Runs client side tests in a headless browser
  search <package>                       Search a package name in the Skypack api, this will bring potential results
  show <package>                         Shows information about a package if the name matches an existing one
  add, install <package>                 Shows information about a package if the name matches an existing one
  remove <package>                       removes a package from the
  list, ls                               Lists the current dependencies in a table or an npm style json string
  regenerate, restore                    Restore the import map based on the selected mode, defaults to production
  create, generate, n, new <name>        Creates a new project based on the selected template if it exists

```

## Existing tools

If you actually use and like nodejs, then you would be better taking a look at the tools that inspired this repository

- [vite](https://vitejs.dev/)
- [snowpack](https://www.snowpack.dev/)

These tools have a bigger community and rely on an even bigger ecosystem plus they support plugins via npm so if you're using node stick with them they are a better choice
Perla's unbundled development was inspired by both snowpack and vite, CDN dependencies were inspired by snowpack's remote sources development
