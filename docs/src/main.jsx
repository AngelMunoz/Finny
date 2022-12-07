import "./index.css";
//@ts-ignore
import { render } from "preact";
import { App } from "./App.js";

//@ts-expect-error
import { setBasePath } from "@shoelace-style/shoelace/dist/utilities/base-path.js";

setBasePath(
  "https://cdn.jsdelivr.net/npm/@shoelace-style/shoelace/shoelace@2.0.0-beta.85/dist/"
);

async function main() {
  await Promise.allSettled([
    //@ts-ignore
    import("@shoelace-style/shoelace"),
  ]).then((args) => console.debug("imported dependencies"));
  render(<App />, document.getElementById("root"));
}

main();
