[perla samples]: https://github.com/AngelMunoz/perla-templates

# Project Structure

Once you have Perla [up and running](/#/content/install), you can pick a template from the [Perla Samples] and simply type `perla serve` for your dev server to start working.

Perla has a set of defaults which allow you to keep a very basic structure, in reality you just need an `index.html` and an empty `perla.jsonc` file but the most ideal structure would be something like this

```sh
perla.jsonc
index.html
src/
  index.js
```

With that, Perla will know what should be served and where

- `perla.jsonc` must have at least these fields

```json
{
  "$schema": "https://raw.githubusercontent.com/AngelMunoz/Perla/v0.14.0/perla.schema.json",
  "index": "./index.html"
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
    <!-- data-entry-point is very important when building for production
         this attribute tells perla that it must process the file when building
         you can include multiple `data-entry-pont` scripts -->
    <script data-entry-point type="module" src="./src/index.js"></script>
  </body>
</html>
```

- `index.js` should be a JS module
