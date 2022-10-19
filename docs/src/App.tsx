import "./App.css";
import { useEffect, useState } from "react";
import {
  SlButton,
  SlDrawer,
  //@ts-ignore
} from "@shoelace-style/shoelace/dist/react/index.js";
import { Page } from "./router.js";
import { Index } from "./Components/Index.js";
import { ToC } from "./Components/ToC.js";
import { MarkdownContent } from "./Components/MarkdownContent.js";

function OffCanvas({
  isOpen,
  onClose,
}: {
  isOpen?: boolean;
  onClose?: () => void;
}) {
  return (
    <SlDrawer
      label="Table of Contents"
      open={isOpen}
      placement="start"
      onSlAfterHide={() => onClose?.()}
    >
      <ToC />
      {onClose ? (
        <SlButton slot="footer" type="primary" onClick={() => onClose()}>
          Close
        </SlButton>
      ) : null}
    </SlDrawer>
  );
}

function Navbar({ requestMenu }: { requestMenu?: () => void }) {
  return (
    <>
      <nav className="perla-nav with-box-shadow">
        <section>
          <SlButton
            className="menu-btn"
            type="text"
            size="large"
            onClick={() => requestMenu?.()}
          >
            Menu
          </SlButton>
          <SlButton href="/#/" type="text" size="large">
            Perla
          </SlButton>
        </section>
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
  const [isOpen, setIsOpen] = useState(false);

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

  return [
    <Navbar requestMenu={() => setIsOpen(true)} />,
    <OffCanvas isOpen={isOpen} onClose={() => setIsOpen(false)} />,
    <main>{content}</main>,
    <footer></footer>,
  ];
}

export default App;
