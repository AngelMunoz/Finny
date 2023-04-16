# Perla Dev Server [![wakatime](https://wakatime.com/badge/user/4537232c-b581-465b-9604-b10a55ffa7b4/project/d46e17c5-054e-4249-a2ab-4294d0e5e026.svg)](https://wakatime.com/@Daniel_Tuna/projects/ktwssuwnmk)

[![Open in Gitpod](https://gitpod.io/button/open-in-gitpod.svg)](https://gitpod.io/#https://github.com/AngelMunoz/Perla)

Perla is a cross-platform single executable binary CLI Tool for a Development Server of Single Page Applications (like vite/webpack but no node required!).

If that sounds like something nice, [Check The docs!](https://perla-docs.web.app/)

## Status

The current **_v1.0.0_** development is taking place in the `dev` branch, please check it out, also the releases tab contains the most recen betas to try out!

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
  add, install <package>                 Shows information about a package if the name matches an existing one
  remove <package>                       removes a package from the
  list, ls                               Lists the current dependencies in a table or an npm style json string
  regenerate, restore                    Restore the import map based on the selected mode, defaults to production
  create, generate, n, new <name>        Creates a new project based on the selected template if it exists

```

## Existing tools

If you actually use and like nodejs, then you would be better taking a look at the tools that inspired this repository

- [jspm](https://github.com/jspm/jspm-cli) - Import map handling, they are the best at manipulating import maps :heart:
- [vite](https://vitejs.dev/)
- [snowpack](https://www.snowpack.dev/)

These tools have a bigger community and rely on an even bigger ecosystem plus they support plugins via npm so if you're using node stick with them they are a better choice
Perla's unbundled development was inspired by both snowpack and vite, CDN dependencies were inspired by snowpack's remote sources development
