import { html, render } from 'lit-html';
import { TsSample } from './file.js';


function Sample() {
    return html`<p>hello there!</p>`;
}
const litApp = document.querySelector("#lit-app");
const litApp2 = document.querySelector("#lit-app-2");
render(Sample(), litApp);
render(TsSample(10, 'c'), litApp2);
