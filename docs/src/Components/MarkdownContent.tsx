import "./MarkdownContent.css";
//@ts-ignore
import { useEffect } from "preact/hooks";
//@ts-ignore
import { useSignal } from "@preact/signals";
import { fetchMarkdown } from "../markdown.js";
import { buildUrl } from "../utils.js";
import { ToC } from "./ToC.js";

const errorContent = (error: string) => [
  <p>Well, Well, Well... How the turntables have turned...</p>,
  <p>{error}</p>,
];

const getUrlForPage = (filename: string, section?: string) => {
  let url =
    "https://github.com/AngelMunoz/Perla/edit/main/src/docs/assets/docs";
  if (section) {
    url += `/${section}`;
  }
  url += `/${filename}.md`;
  return url;
};

export function MarkdownContent({ filename, section }: MarkdownContentProps) {
  const content = useSignal("");
  const error = useSignal("");

  useEffect(() => {
    const url = buildUrl(filename, section);
    fetchMarkdown(url)
      .then((response) => (content.value = response))
      .catch((err) => {
        content.value = "";
        error.value = err;
      });
  });

  return (
    <article className="markdown-page">
      <ToC isAside={true} />
      <section className="markdown-content__section">
        <header className="markdown-content__header">
          <sl-button
            type="text"
            target="_blank"
            href={getUrlForPage(filename, section)}
          >
            Edit this page
          </sl-button>
        </header>
        {content.value ? (
          <article
            className="markdown-content"
            dangerouslySetInnerHTML={{ __html: content.value }}
          ></article>
        ) : (
          <article className="markdown-content markdown-error">
            {error.value ? errorContent(error.value) : null}
          </article>
        )}
      </section>
    </article>
  );
}
