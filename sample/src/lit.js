import { html, render, LitElement } from "lit";
import styles from "./csmod.css?assertion" assert { type: "css" };

class MySampleEl extends LitElement {
  static styles = [styles];
  render() {
    return html`
      <header>This is a Lit Web Component</header>
      <section>Using CSS Module Assertions :)</section>
    `;
  }
}

window.customElements.define("my-sample-el", MySampleEl);
