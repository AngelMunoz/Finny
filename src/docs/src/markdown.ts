import MarkdownIt from 'markdown-it';
//@ts-ignore
import hljs from 'highlight.js/lib/core.js';
//@ts-ignore
import javascript from 'highlight.js/lib/languages/javascript.js';
//@ts-ignore
import text from 'highlight.js/lib/languages/plaintext.js';
//@ts-ignore
import fsharp from 'highlight.js/lib/languages/fsharp.js';
//@ts-ignore
import bash from 'highlight.js/lib/languages/bash.js';
//@ts-ignore
import json from 'highlight.js/lib/languages/json.js';
//@ts-ignore
import xml from 'highlight.js/lib/languages/xml.js';

hljs.registerLanguage('', text);
hljs.registerLanguage('javascript', javascript);
hljs.registerLanguage('fsharp', fsharp);
hljs.registerLanguage('bash', bash);
hljs.registerLanguage('json', json);
hljs.registerLanguage('html', xml);



const md = new MarkdownIt({
    typographer: true,
    linkify: true,
    html: true,
    highlight(str, language, attrs) {
        return hljs.highlight(str, { language }).value;
    }
});


export async function fetchMarkdown(url: string) {
    const res = await fetch(url);
    if (res.ok) {
        const content = await res.text();
        return md.render(content);
    }
    return Promise.reject(`${res.status} - ${res.statusText}`);
}
