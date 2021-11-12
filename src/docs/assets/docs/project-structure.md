[perla samples]: https://github.com/AngelMunoz/perla-samples

# Project Structure

Once you have Perla [up and running](/#/content/install), you can pick a template from the [Perla Samples] and simply type `perla serve` for your dev server to start working.

Here's the most basic Perla project structure

```sh
perla.jsonc
index.html
src/
  index.js
```

- `perla.jsonc` must have at least these fields

```json
{
  "$schema": "https://raw.githubusercontent.com/AngelMunoz/Perla/v0.14.0/perla.schema.json",
  "index": "./index.html",
  "devServer": {
    "mountDirectories": {
      "./src": "/src"
    }
  },
  "packages": {
    "react": "https://cdn.skypack.dev/pin/react@v17.0.1-yH0aYV1FOvoIPeKBbHxg/mode=imports/optimized/react.js",
    "react-dom": "https://cdn.skypack.dev/pin/react-dom@v17.0.1-oZ1BXZ5opQ1DbTh7nu9r/mode=imports/optimized/react-dom.js"
  }
}
```

- `index.html` must have at least these fields

```html
<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <link rel="icon" type="image/svg+xml" href="./src/favicon.ico" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Perla App</title>
    <!-- This shim is required for import maps to work propperly-->
    <script
      async
      src="https://ga.jspm.io/npm:es-module-shims@1.0.0/dist/es-module-shims.js"
      crossorigin="anonymous"
    ></script>
  </head>
  <body>
    <div id="root"></div>
    <script data-entry-point type="module" src="./src/index.js"></script>
  </body>
</html>
```

- `index.js` should be a JS module
