import { html, render, LitElement } from 'lit'
import { TsSample } from './file.js';
import styles from './csmod.css?module' assert { type: 'css' };

class MySampleEl extends LitElement {
    static get styles() {
        return [styles]
    }
    render() {
        return html`<section>Some Text :)</section>`
    }
}

customElements.define("my-sample-el", MySampleEl)

function Sample() {
    return html`
        <p>hello there!</p>
        <my-sample-el></my-sample-el>
    `;
}

const litApp = document.querySelector("#lit-app");
const litApp2 = document.querySelector("#lit-app-2");
render(Sample(), litApp);
render(TsSample(10, 'c'), litApp2);
