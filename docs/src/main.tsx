import { StrictMode } from "react";
import ReactDOM from "react-dom";

import "./index.css";
import App from "./App.js";

//@ts-expect-error
import { setBasePath } from "@shoelace-style/shoelace/dist/utilities/base-path.js";

setBasePath(
  "https://cdn.jsdelivr.net/npm/@shoelace-style/shoelace@2.0.0-beta.60/dist/"
);

ReactDOM.render(
  <StrictMode>
    <App />
  </StrictMode>,
  document.getElementById("root")
);
