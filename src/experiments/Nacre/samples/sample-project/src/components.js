import { LitElement, html } from "https://jspm.dev/lit";

export class MyElement extends LitElement {
    constructor() {
        super();
        this.prop = 10;
    }

    render() {
        return html`<div data-value=${this.prop}></div>`;
    }
}
customElements.define("my-element", MyElement);

