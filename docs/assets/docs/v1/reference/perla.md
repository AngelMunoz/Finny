[import map]: /#/content/import-maps
[json schema]: https://github.com/AngelMunoz/Perla/blob/main/perla.schema.json
[json schemas]: https://json-schema.org/

## perla.json and perla.json.importmap

The `perla.json` file es the main configuration file with this file you an control most of the Perla CLI.

The `perla.json.importmap` is the actual [import map] used by your application in both development and production
You can write comments on this file to keep tabs on why are things the way they are.

We offer a [JSON schema] for the `perla.json` file so you can get autocompletition in editors like VSCode and any other that supports [JSON Schemas]

Most of the options in the `perla.json` file are optional, we have set defaults already for these properties.

> If you set any property in the `perla.json` file you will override the defaults, we don't do any kind of _merge_ strategy on objects or arrays, so if you do it you need to provide the whole object. If this is a concern for you please raise an issue to be aware of it.

A full `perla.json` file looks like this:

```json
{
  // we tag each release on github if you're running a particular version of perla you can
  // use the git tag version of the schema
  "$schema": "https://raw.githubusercontent.com/AngelMunoz/Perla/dev/perla.schema.json",
  "index": "./index.html",
  "provider": "jspm",
  "runConfiguration": "production",
  // mount local directories on specific URL paths
  "mountDirectories": {
    // resources under ./src will be available in /src URL
    "/src": "./src",
    // e.g. ./assets/docs/index.md -> /public/docs/indexmd
    "/assets": "./public",
    // useful for service workers
    "/": "./workers"
  },
  // If you're using perla plugins list the order of execution,
  // if you're not using anything you can omit this value
  // but the "perla-esbuild-plugin" value must always be there
  // for perla to be able to process css/js/tsx/jsx/ts files
  "plugins": ["perla-esbuild-plugin"],
  // pass environment variables to perla
  "enableEnv": true,
  // where to import these variables from
  "envPath": "/env.js",
  // list of packages you will pull from the specified provider
  "dependencies": [
    // these are actual dependencies that your app needs to work
    { "name": "lodash", "version": "4.17.15" }
  ],
  "devDependencies": [
    // list of dependencies you want to use at dev/testing time only
    { "name": "rxjs-spy", "version": "8.0.2" },
    // assertions with chai for example
    { "name": "@esm-bundle/chai", "version": "4.3.4" }
  ],
  "fable": {
    // F# project to compile
    "project": "./src/App.fsproj",
    // output extension of the fable files
    "extension": ".fs.js",
    // enable Fable source map output
    "sourceMaps": true,
    // where to output these files
    "outDir": "path/to/dist"
  },
  "devServer": {
    // port to run the dev server on
    "port": 7331,
    // host to run the dev server like localhost or 0.0.0.0
    "host": "127.0.0.1",
    // enable reload on change for sources
    "liveReload": true,
    // use HTTPs by default
    "useSSL": true,
    // add a dev proxy for  server requests
    "proxy": {
      // proxy anything request that targets /api/ to localhost on port 5000
      "/api/{**catch-all}": "http://localhost:5000",
      // this can be used for web sockets as well
      // proxy calls to /ws to /sockets/ws
      "/ws": "http://localhost:5000/sockets"
    }
  },
  // these settings can be used to fine-tune certain esbuild options
  "esbuild": {
    // do you have a custom esbuild version? you can point at it
    "esBuildPath": "/path/to/esbuild",
    // pin the esbuild version you want to use in case our default is not up to date
    "version": "0.15.16",
    // Allow your code to compile down to better supported spec
    "ecmaVersion": "es2020",
    // if you need to debug output code, this setting might be handy
    "minify": true,
    // injects will only run at build time
    // and are injected into every file processed by esbuild
    "injects": ["./license.js"],
    "externals": [
      // mark a dependency as external and don't include it in the
      // esbuild bundling process example:
      // import config from '/api/config.js'
      "/api/config.js"
      // esbuild would usually try to look at the contents of config.js and bundle them
      // in this case it is left alone and will be present in the bundle's output
    ],
    // specify esbuild loaders for extensions
    "fileLoaders": {
      ".png": "file",
      ".svg": "file",
      ".woff": "file",
      ".woff2": "file"
    },
    // use the following options to configure jsx transforms
    // configure preact for example
    "jsxAutomatic": true,
    "jsxImportSource": "preact"
  },
  "build": {
    // globbing patterns that specify something has to be copied
    // from the local or virtual file system over the build's output
    "includes": [
      // include compiled HTML files from the virtual file system
      "vfs:**/**/*.html",
      // copy all of the markdown files from the local file system
      "**/**/*.md"
    ],
    // globbing patterns that specify something has to be copied
    // from the local or virtual file system over the build's output
    "excludes": [
      "./**/obj/**",
      "./**/bin/**",
      "./**/*.fs",
      "./**/*.fsproj",
      // example for the virtual file system
      "vfs:**/.spec.js"
    ],
    // where should the build output its result
    "outDir": "./dist",
    // if you don't want to emit an environment file
    // because you might have an endpoint that does that already
    "emitEnvFile": true
  },
  "testing": {
    "headless": true,
    // u
    "watch": false,
    // run the test suites for each browser in parallel
    "browserMode": "parallel",
    // which browsers to run the tests against
    "browsers": ["chromium"],
    // similarly to the build's includes/excludes but this applies for testing files
    // these must be valid javascript files
    "excludes": [],
    "includes": ["**/*.test.js", "**/*.spec.js"]
  }
}
```
