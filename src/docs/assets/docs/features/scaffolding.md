[scriban]: https://github.com/scriban/scriban
[scriban templates]: https://github.com/scriban/scriban/blob/master/doc/language.md

## Scaffolding

Getting started with Perla projects is very simple

- If you just downloaded/installed perla for the first time run:

  - `perla init -k full` (optionally pass `--yes true` to skip prompts)

  That will download some perla assets to ensure you can start developing right away.

- If you already have templates but you want to add a new set of templates run:

  - `perla add-template -n GitHubUsername/repository -b branch`
    - Example: `perla add-template -n AngelMunoz/perla-samples -b main`

  That will ensure you have a set of templates available for you to start developing.

Once you have templates in place you can start creating new perla projects using `perla new`, using `perla-samples` as our example repository we can create a new project doing any of the following actions:

- `perla new -t perla-samples/react-jsx -n my-react-project`
- `perla new -t perla-samples/vue-jsx -n my-react-project`
- `perla new -t perla-samples/lit-js -n my-lit-project`
- `perla new -t perla-samples/fable-feliz -n my-feliz-project`
- `perla new -t perla-samples/fable-sutil -n my-sutil-project`

A full initial workflow would look like this

```sh
# download/install perla
perla init -k full -y true
perla new -t perla-samples/react-jsx -n my-react-project
cd my-react-project
perla serve
# after you're done with your website
perla build
```

## Creating your own templates

Creating your own templates is very simple:

1. Create a github repository
2. Add a new directory
3. Add the file structure/project files needed

Perla's scaffolding features are based on conventions, each directory on the root repository means that is a different template, the directory name is the name of the template, from there on when perla creates a new project it simply copies the contents of that directory into the new location

## Extending templates

Sometimes, you want to change certain parts of your template based on user input for that we allow you to use F# scripts and [scriban] (like handlebars, mustache or liquid) templates, Let's do a step by step example

1. Create a github repository
2. Add a new directory named my-template
   1. Add index.tpl.html
   2. Add perla.jsonc
   3. Add ./src/main.js
   4. Add templating.fsx
3. upload to github
4. `perla add-template Username/repository -b main`
5. `perla new -t repository/my-template -n my-new-project`
6. Answer the prompts we added in the F# script
7. `perla serve`

the content of the `index.tpl.html` would look like this:

> Note: `.tpl.` must exist in the file name for perla to pick it up

```html
<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <link rel="icon" type="image/svg+xml" href="/src/favicon.svg" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>{{ title }}</title>
    <script
      async
      src="https://ga.jspm.io/npm:es-module-shims@1.0.0/dist/es-module-shims.js"
      crossorigin="anonymous"
    ></script>
    {{ if include_bulma }}
    <link
      rel="stylesheet"
      href="https://cdn.jsdelivr.net/npm/bulma@0.9.3/css/bulma.min.css"
    />
    {{ end }}
  </head>
  <body>
    <script data-entry-point type="module" src="./src/main.js"></script>
  </body>
</html>
```

`perla.jsonc`

```json
{
  "$schema": "https://raw.githubusercontent.com/AngelMunoz/Perla/main/perla.schema.json",
  "index": "./index.html"
}
```

the javascript file would be:

```javascript
console.log("Hello world!");
```

Finally the F# script `templating.fsx` would have this content:

```fsharp
open System

printfn "Project Name: (My Project)"

let title =
    match Console.ReadLine() with
    // the user hit enter without a project name
    | "" -> "My Project"
    // the user wrote a project name
    | title -> title

printfn "Include bulma css? [N/y]"

let include_bulma =
    match Console.ReadKey().Key with
    // the user pressed Y on the keyboard
    | ConsoleKey.Y -> true
    // the user pressed other key on the keyboard
    | _ -> false

// THIS IS THE IMPORTANT VALUE
// Ensure your script always includes this value
// with this precise name or otherwise perla won't
// know what to do with the templating files
let TemplateConfiguration =
    {| title = title
       include_bulma = include_bulma |}
```

> **_Note_**: The `TemplateConfiguration` value **MUST** be somewhere in the script because once the script is evaluated perla will look for that value and use it to compile the templates.

Once you push the template into github you should be able to follow the last steps we wrote above.

You can make any kind of file a template by adding `.tpl.` not just html files, when perla sees that in a filename it knows it needs to compile it using [scriban templates].
