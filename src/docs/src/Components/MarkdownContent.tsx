import "./MarkdownContent.css";
import { useEffect, useState } from "react";
import { fetchMarkdown } from "../markdown.js";
import { buildUrl } from "../utils.js";

//@ts-ignore
import { SlButton } from "@shoelace-style/shoelace/dist/react/index.js";
//@ts-ignore
import _toc from "../toc.json?module";

const toc: ToC = _toc;

const getEntryTpl = (section: ToCSection) => {
  const getEntriesForSection = ({ title, url }: ToCLink) => {
    return (
      <li>
        <SlButton type="text" href={url}>
          {title}
        </SlButton>
      </li>
    );
  };
  return (
    <section>
      <h4>{section.label}</h4>
      <ul className="link-list">
        {section.sections.map(getEntriesForSection)}
      </ul>
    </section>
  );
};

function ToC() {
  return (
    <aside className="markdown-aside">
      {Object.entries(toc).map(([, section]) => getEntryTpl(section))}
    </aside>
  );
}

const errorContent = (error: string) => [
  <p>Well, Well, Well... How the turntables have turned...</p>,
  <p>{error}</p>,
];

export function MarkdownContent({ filename, section }: MarkdownContentProps) {
  const [content, setContent] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    const url = buildUrl(filename, section);
    fetchMarkdown(url)
      .then(setContent)
      .catch((err) => {
        setContent("");
        setError(err);
      });
  });

  return (
    <article className="markdown-page">
      <ToC />
      {content ? (
        <article
          className="markdown-content"
          dangerouslySetInnerHTML={{ __html: content }}
        ></article>
      ) : (
        <article className="markdown-content markdown-error">
          {error ? errorContent(error) : null}
        </article>
      )}
    </article>
  );
}
