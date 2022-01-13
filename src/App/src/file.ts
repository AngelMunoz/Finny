import { html } from 'lit-html';


export function TsSample(value: number, kind: Kind) {
    return html`
        <div>
            <h1>Hello From Typescript!</h1>
            <p>The path is ${location.pathname}</p>
            <p>Vaue: ${value}, Kind: ${kind}</p>
        </div>
    `;
}


export type Kind = 'a' | 'b' | 'c';
