import { marked } from "marked";
//@ts-ignore
import hljs from "highlight.js/lib/core";
//@ts-ignore
import javascript from "highlight.js/lib/languages/javascript";
//@ts-ignore
import text from "highlight.js/lib/languages/plaintext";
//@ts-ignore
import fsharp from "highlight.js/lib/languages/fsharp";
//@ts-ignore
import bash from "highlight.js/lib/languages/bash";
//@ts-ignore
import json from "highlight.js/lib/languages/json";
//@ts-ignore
import xml from "highlight.js/lib/languages/xml";

hljs.registerLanguage("", text);
hljs.registerLanguage("javascript", javascript);
hljs.registerLanguage("fsharp", fsharp);
hljs.registerLanguage("bash", bash);
hljs.registerLanguage("json", json);
hljs.registerLanguage("html", xml);

marked.setOptions({
  smartLists: true,
  smartypants: true,
  headerIds: true,
  langPrefix: "hljs language-",
  highlight(code, language) {
    return hljs.highlight(code, { language }).value;
  },
});

export async function fetchMarkdown(url: string) {
  const res = await fetch(url);
  if (res.ok) {
    const content = await res.text();
    return marked.parse(content);
  }
  return Promise.reject(`${res.status} - ${res.statusText}`);
}
