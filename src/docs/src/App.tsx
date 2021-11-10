import "./App.css";
import { useEffect, useState } from "react";
import { Page, Router } from "./router.js";
import { fetchMarkdown } from "./markdown.js";
import { Index } from "./Components/Index.js";
import { buildUrl } from "./utils.js";

function MarkdownContent({ filename, section }: MarkdownContentProps) {
  const [content, setContent] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    const url = buildUrl(filename, section);
    fetchMarkdown(url).then(setContent).catch(setError);
  });

  return content ? (
    <article
      className="markdown"
      dangerouslySetInnerHTML={{ __html: content }}
    ></article>
  ) : (
    <article className="markdown markdown-error">{error}</article>
  );
}

function Navbar() {
  return (
    <>
      <nav className="perla-nav">
        <div>Logo</div>
        <section className="nav-links">
          <ul className="link-list">
            <li>Docs</li>
            <li>Blog</li>
          </ul>
          <ul className="link-list">
            <li>Github</li>
            <li>Twitter</li>
          </ul>
        </section>
      </nav>
    </>
  );
}
//<MarkdownContent filename={ data?.filename } section = { data?.section } />
function App() {
  const [content, setContent] = useState(<Index />);

  useEffect(() => {
    const sub = Page.subscribe((page) => {
      if (page === "Home") {
        setContent(<Index />);
      } else {
        const [_, section, pageName] = page;
        setContent(<MarkdownContent filename={pageName!} section={section} />);
      }
    });
    return () => sub.unsubscribe();
  }, []);

  return [<Navbar />, <main>{content}</main>, <footer></footer>];
}

export default App;
