[package manager]: /#/docs/features/package-manager

# Import Maps

> For a full reference on Import Maps Check the Import Maps Repository: https://github.com/WICG/import-maps

Rather than relying on local NPM packages, the approach we took with Perla is different, we try to rely on the browser as much as possible.

Import Maps are a way to tell the browser how to load dependencies as the import-maps repository says:

> This proposal allows control over what URLs get fetched by JavaScript import statements and import() expressions. This allows "bare import specifiers", such as import moment from "moment", to work.
>
> The mechanism for doing this is via an import map which can be used to control the resolution of module specifiers generally. As an introductory example, consider the code

Our "[package manager]" is nothing but a collector of URL's which generate an import map that is injected in the `index.html` at dev and build time, you will be able to find this import map in your repository under the name of `perla.jsonc.importmap`. For example this very documentation website (which is dogfooding Perla) has an _import map file_ that looks like the following:

```json
{
  "imports": {
    // allows you to do import { dependency } from '@shoelace-style/shoelace';
    "@shoelace-style/shoelace": "...url...",
    // allows you to do import { dependency } from '@shoelace-style/shoelace/dist/react/dependency.js';
    // this entry was written manually
    "@shoelace-style/shoelace/dist/react/": "...url...",
    // allows you to do import { dependency } from '@shoelace-style/shoelace/dist/utilities/dependency.js';
    // this entry was written manually
    "@shoelace-style/shoelace/dist/utilities/": "...url...",
    // this entry was written manually
    "highlight.js/lib/": "...url...",
    // this entry was written manually
    "highlight.js/lib/languages/": "...url...",
    // import markdownit from 'markdown-it'
    "markdown-it": "...url...",
    // import router from 'navigo'
    "navigo": "...url...",
    "react": "...url...",
    "react-dom": "...url...",
    "rxjs": "...url..."
  },
  "scopes": {
    // Some sources like JSPM have internal scopes that allow for nested dependencies
    // to import more information themselves
    // these URL's are scoped and only have access to that specific resource
    "https://ga.jspm.io/": {
      "entities/lib/maps/entities.json": "...url...",
      "linkify-it": "...url...",
      "mdurl": "...url...",
      "punycode": "...url...",
      "uc.micro": "...url...",
      "uc.micro/categories/Cc/regex": "...url...",
      "uc.micro/categories/P/regex": "...url...",
      "uc.micro/categories/Z/regex": "...url...",
      "uc.micro/properties/Any/regex": "...url..."
    }
  }
}
```

This means that we keep track of what you "install" but in case there are packages that have internal dependencies in their packages you can manually route them to be able to pull anything you need from them , that's what we do for `highlight.js/lib/` and `highlight.js/lib/languages/` thisa helps you as well to reduce bundle sizes in production.
