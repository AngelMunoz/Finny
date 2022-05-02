# Perla Dev Server

[![Open in Gitpod](https://gitpod.io/button/open-in-gitpod.svg)](https://gitpod.io/#https://github.com/AngelMunoz/Perla)

Perla is a cross-platform single executable binary CLI Tool for a Development Server of Single Page Applications.

If that sounds like something nice, [Check The docs!](https://perla-docs.web.app/)

## Status

> take a peek of PoC's and new stuff that may (or not) be coming to perla in the [experiments branch](https://github.com/AngelMunoz/Perla/tree/experiments-types)

> ### Taking a break in the first half of 2022
> 
> To prevent heavy burnouts, I'm taking a break in 2022. for very very basic projects this should take you a long way, but if you want to work with more complicated frontend files like vue files, svelte files or similar things, its not there yet, basically if its just HTML/CSS/JS (Or Fable based projects) you can give it a shot

This project is in development, current goals at this point are:

- [x] Remove npm/node out of the equation.
- [x] For F# users, seamless fable integration.
- [x] A Fast and easy to use Development server
- [x] Build for production using esbuild.
- [x] Binary Release for users outside .NET
- [ ] HMR (for other than CSS)
- [ ] Plugin System

For more information check the Issues tab.

## Existing tools

If you actually use and like nodejs, then you would be better taking a look at the tools that inspired this repository

- [vite](https://vitejs.dev/)
- [snowpack](https://www.snowpack.dev/)

These tools have a bigger community and rely on an even bigger ecosystem plus they support plugins via npm so if you're using node stick with them they are a better choice
Perla's unbundled development was inspired by both snowpack and vite, CDN dependencies were inspired by snowpack's remote sources development
