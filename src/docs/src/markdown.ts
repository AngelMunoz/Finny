import MarkdownIt from 'markdown-it';

const md = new MarkdownIt({
    typographer: true,
    linkify: true,
    html: true
});


export function renderMd(content: string) {
    return md.render(content);
}

export async function fetchMarkdown(url: string) {
    const res = await fetch(url);
    if (res.ok) {
        const content = await res.text();
        return renderMd(content);
    }
    return Promise.reject(`${res.status} - ${res.statusText}`);
}
