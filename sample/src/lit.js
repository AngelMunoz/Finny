import { html, LitElement } from "lit";

class MySampleEl extends LitElement {
  render() {
    return html` <header>This is a Lit Web Component</header> `;
  }
}

window.customElements.define("my-sample-el", MySampleEl);
