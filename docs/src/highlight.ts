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
//@ts-ignore
import diff from "highlight.js/lib/languages/diff";

hljs.registerLanguage("", text);
hljs.registerLanguage("javascript", javascript);
hljs.registerLanguage("fsharp", fsharp);
hljs.registerLanguage("bash", bash);
hljs.registerLanguage("json", json);
hljs.registerLanguage("html", xml);
hljs.registerLanguage("diff", diff);

const parser = new DOMParser();
export async function fetchDocs(url: string) {
  const response = await fetch(url).then((response) => {
    if (!response.ok) {
      return Promise.reject(new Error(response.statusText));
    }
    return response.text();
  });
  const elements = parser.parseFromString(response, "text/html");
  elements?.querySelectorAll?.("pre code")?.forEach?.((element) => {
    if (!element) return;
    hljs.highlightElement(element);
  });
  return elements.body.innerHTML;
}
