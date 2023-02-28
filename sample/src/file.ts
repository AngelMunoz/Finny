import { html } from "lit";
import { until } from "lit/directives/until.js";
// if it doesn't exist it will throw an error
// import { envValue, SOME_TOKEN } from '/env.js'

async function lazyComponent() {
  try {
    var { envValue, SOME_TOKEN } = await import("/env.js");
  } catch (error) {
    envValue = "not provided";
    SOME_TOKEN = "not provided";
  }
  return html`
    <p style="color: var(--danger-color)">
      Env Vars! <b>${envValue}</b>, <b>${SOME_TOKEN}</b>
    </p>
  `;
}

export function TsSample(value: number, kind: Kind) {
  return html`
    <div>
      <h1 style="color: var(--primary-color)">Hello From Typescript!</h1>
      <p style="color: var(--primary-color)">
        The path is ${location.pathname}
      </p>
      <p style="color: var(--link-color)">Vaue: ${value}, Kind: ${kind}</p>
      ${until(lazyComponent())}
    </div>
  `;
}

export type Kind = "a" | "b" | "c";
