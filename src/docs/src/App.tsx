import "./App.css";
import { useEffect, useState } from "react";
//@ts-ignore
import { SlButton } from "@shoelace-style/shoelace/dist/react/index.js";
import { Page } from "./router.js";
import { Index } from "./Components/Index.jsx";
import { MarkdownContent } from "./Components/MarkdownContent.jsx";

function Navbar() {
  return (
    <>
      <nav className="perla-nav">
        <div>
          <SlButton href="/#/" type="text" size="large">
            Perla
          </SlButton>
        </div>
        <section className="nav-links">
          <ul className="link-list">
            <li>
              <SlButton href="/#/content/index" type="text">
                Docs
              </SlButton>
            </li>
            <li>
              <SlButton href="/#/blog" type="text">
                Blog
              </SlButton>
            </li>
            <li>
              <SlButton
                target="_blank"
                href="https://github.com/AngelMunoz/Perla"
                type="text"
              >
                Github
              </SlButton>
            </li>
          </ul>
        </section>
      </nav>
    </>
  );
}

function App() {
  const [content, setContent] = useState(<Index />);

  useEffect(() => {
    const sub = Page.subscribe((page: Page) => {
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
