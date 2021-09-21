import { html, render } from 'lit-html';


function Sample() {
    return html`<p>hello there!</p>`;
}

render(Sample(), document.querySelector("#lit-app"));
