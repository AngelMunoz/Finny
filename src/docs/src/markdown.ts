import MarkdownIt from 'markdown-it';
import hljs from 'highlight.js/lib/core.js';
import javascript from 'highlight.js/lib/languages/javascript.js';
import fsharp from 'highlight.js/lib/languages/fsharp.js';
import bash from 'highlight.js/lib/languages/bash.js';
import json from 'highlight.js/lib/languages/json.js';
import xml from 'highlight.js/lib/languages/xml.js';

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
