[saturn]: https://saturnframework.org/
[asp.net]: https://dotnet.microsoft.com/apps/aspnet

# Development Server

Perla is built using [Saturn] which in turn is build with [asp.net] making it a blazing fast development server.

by default Perla uses these options if you don't specify them in the `perla.json` > `devServer` object

- autoStart - true

  This means that the saturn server should start as soon as the `perla serve` command is entered.

- port - 7331
- host - localhost
- mountDirectories

  The mount directories object provides a way for Perla to know which directories will be used to provide content and what will be copied into the final dev build

  ```json
  // mount the ./src directory on the /src url path
  { "./src": "/src" }
  ```

  for example: you could provide an "_assets_" directory to mount all of the images or other kinds of files in your project and serve them under "_/assets_" url.

  ```json
  { "./src": "/src", "./assets": "/assets" }
  ```

- watchConfig

  For a dev server to be useful it needs to react to the changes of your source code and reflect them as soon as possible, Perla does this by looking at some extensions and directories

  ```json
  // watch the ./src directory AND the specified extensions
  { "extensions":
      ["*.js", "*.css", "*.ts", "*.tsx"; "*.jsx", "*,json"],
    "directories": ["./src"] }
  ```

- liveReload - true
- useSSL - false
- enableEnv - true
- envPath - /env.js

> It's worth mentioning that once you override one of these configurations we don't do any kind of "merge" strategy, so if you override the extensions to add a new one, you need to copy the whole array you also need to provide the "directories" array or otherwise you won't watch anything

## Environment Variable Support

Perla supports environment variables at dev time prefixed with `PERLA_`.

As an example let's think about the following environment variables

```bash
PERLA_clientToken=abcdefg1234557
PERLA_API_KEY=12334566abcdefg
```

Perla will use `enableEnv` and `envPath` nodes from the `devServer` options and provide a javascript file with those environment variables, something like

```js
export const clientToken = "abcdefg1234557";
export const API_KEY = "12334566abcdefg";
```

so you can do the following in your code

```js
import { clientToken, API_KEY } from "/env.js";
```

or in Fable Projects

```fsharp
open Fable.Core
open Fable.Core.JsInterop

let API_KEY = import "API_KEY" "/env.js"
// or the named import option
let clientToken = importMember "/env.js"
```

at build time we don't emit anything for security reasons

> **NOTE**: If you're using TS/JSX/TSX then you might want to enable `"preserveValueImports": true` in your tsconfig.json if for some reason the import is not working at dev time
