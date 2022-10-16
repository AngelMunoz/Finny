# Perla Dev Server [![wakatime](https://wakatime.com/badge/user/4537232c-b581-465b-9604-b10a55ffa7b4/project/d46e17c5-054e-4249-a2ab-4294d0e5e026.svg)](https://wakatime.com/badge/user/4537232c-b581-465b-9604-b10a55ffa7b4/project/d46e17c5-054e-4249-a2ab-4294d0e5e026)

[![Open in Gitpod](https://gitpod.io/button/open-in-gitpod.svg)](https://gitpod.io/#https://github.com/AngelMunoz/Perla)

Perla is a cross-platform single executable binary CLI Tool for a Development Server of Single Page Applications.

If that sounds like something nice, [Check The docs!](https://perla-docs.web.app/)

## Status

> vNext is on full steam check https://github.com/AngelMunoz/Perla/pull/89 for more information

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
