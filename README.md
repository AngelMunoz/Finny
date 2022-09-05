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

## Existing tools

If you actually use and like nodejs, then you would be better taking a look at the tools that inspired this repository

- [vite](https://vitejs.dev/)
- [snowpack](https://www.snowpack.dev/)

These tools have a bigger community and rely on an even bigger ecosystem plus they support plugins via npm so if you're using node stick with them they are a better choice
Perla's unbundled development was inspired by both snowpack and vite, CDN dependencies were inspired by snowpack's remote sources development
