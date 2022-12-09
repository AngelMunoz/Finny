[package manager]: /#/v1/docs/features/package-manager
[jspm]: https://jspm.io/
[jspm generator]: https://generator.jspm.io/
[jspm generator api]: https://jspm.org/docs/cdn#jspm-generator

# Import Maps

> For a full reference on Import Maps Check the Import Maps Repository: https://github.com/WICG/import-maps

Rather than relying on local NPM packages, the approach we took with Perla is different, we try to rely on the browser as much as possible.

Import Maps are a way to tell the browser how to load dependencies as the import-maps repository says:

> This proposal allows control over what URLs get fetched by JavaScript import statements and import() expressions. This allows "bare import specifiers", such as import moment from "moment", to work.
>
> The mechanism for doing this is via an import map which can be used to control the resolution of module specifiers generally. As an introductory example, consider the code

Our "[package manager]" is nothing but a collector of URL's which generate an import map that is injected in the `index.html` at dev and build time, you will be able to find this import map in your repository under the name of `perla.json.importmap`. For example this very documentation website (which is dogfooding Perla) has an _import map file_ that looks like the following:

```json
{
  "imports": {
    "@preact/signals": "https://ga.jspm.io/npm:@preact/signals@1.1.2/dist/signals.module.js",
    "@preact/signals-core": "https://ga.jspm.io/npm:@preact/signals-core@1.2.2/dist/signals-core.module.js",
    "@shoelace-style/shoelace": "https://ga.jspm.io/npm:@shoelace-style/shoelace@2.0.0-beta.85/dist/shoelace.js",
    "@shoelace-style/shoelace/dist/utilities/base-path.js": "https://ga.jspm.io/npm:@shoelace-style/shoelace@2.0.0-beta.85/dist/utilities/base-path.js",
    "highlight.js": "https://ga.jspm.io/npm:highlight.js@11.7.0/es/index.js",
    "highlight.js/lib/core": "https://ga.jspm.io/npm:highlight.js@11.7.0/es/core.js",
    "highlight.js/lib/languages/bash": "https://ga.jspm.io/npm:highlight.js@11.7.0/es/languages/bash.js",
    "highlight.js/lib/languages/diff": "https://ga.jspm.io/npm:highlight.js@11.7.0/es/languages/diff.js",
    "highlight.js/lib/languages/fsharp": "https://ga.jspm.io/npm:highlight.js@11.7.0/es/languages/fsharp.js",
    "highlight.js/lib/languages/javascript": "https://ga.jspm.io/npm:highlight.js@11.7.0/es/languages/javascript.js",
    "highlight.js/lib/languages/json": "https://ga.jspm.io/npm:highlight.js@11.7.0/es/languages/json.js",
    "highlight.js/lib/languages/plaintext": "https://ga.jspm.io/npm:highlight.js@11.7.0/es/languages/plaintext.js",
    "highlight.js/lib/languages/xml": "https://ga.jspm.io/npm:highlight.js@11.7.0/es/languages/xml.js",
    "navigo": "https://ga.jspm.io/npm:navigo@8.11.1/lib/navigo.min.js",
    "preact": "https://ga.jspm.io/npm:preact@10.11.3/dist/preact.module.js",
    "preact/hooks": "https://ga.jspm.io/npm:preact@10.11.3/hooks/dist/hooks.module.js",
    "preact/jsx-runtime": "https://ga.jspm.io/npm:preact@10.11.3/jsx-runtime/dist/jsxRuntime.module.js",
    "rxjs": "https://ga.jspm.io/npm:rxjs@7.5.7/dist/esm5/index.js",
    "tslib": "https://ga.jspm.io/npm:tslib@2.4.1/tslib.es6.js"
  }
}
```

> **New in V1**: We changed the way import maps are generated, import maps are flattened where possible to make them easier to update

This means that we keep track of what you "install" but in case there are packages that have internal dependencies in their packages you can manually route them to be able to pull anything you need from them , that's what we do for `highlight.js/lib/` and `highlight.js/lib/languages/` thisa helps you as well to reduce bundle sizes in production.

### JSPM

Big shout out to the [JSPM] folks who are making an incredible job of providing reliable software that allows import maps to move forward and get adoption, in perla we leverage the [jspm generator api] to be able to bring import map resolution outside nodejs, please check them out as they are doing the good work that allows others to keep moving forward

> What's the fuzz about import maps? Try the online [JSPM Generator]!
