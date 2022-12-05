import "./App.css";
//@ts-ignore
import { useSignal, signal } from "@preact/signals";
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
    <sl-drawer
      label="Table of Contents"
      open={isOpen}
      placement="start"
      onSlAfterHide={() => onClose?.()}
    >
      <ToC />
      {onClose ? (
        <sl-button slot="footer" variant="primary" onClick={() => onClose()}>
          Close
        </sl-button>
      ) : null}
    </sl-drawer>
  );
}

function Navbar({ requestMenu }: { requestMenu?: () => void }) {
  return (
    <>
      <nav className="perla-nav with-box-shadow">
        <section>
          <sl-button
            className="menu-btn"
            variant="text"
            size="large"
            onClick={() => requestMenu?.()}
          >
            Menu
          </sl-button>
          <sl-button href="/#/" variant="text" size="large">
            Perla
          </sl-button>
        </section>
        <section className="nav-links">
          <ul className="link-list">
            <li>
              <sl-button href="/#/content/index" variant="text">
                Docs
              </sl-button>
            </li>
            <li>
              <sl-button href="/#/blog" variant="text">
                Blog
              </sl-button>
            </li>
            <li>
              <sl-button
                target="_blank"
                href="https://github.com/AngelMunoz/Perla"
                variant="text"
              >
                Github
              </sl-button>
            </li>
          </ul>
        </section>
      </nav>
    </>
  );
}

const content = signal(<Index />);

Page.subscribe((page: Page) => {
  if (page === "Home") {
    content.value = <Index />;
  } else {
    const [_, section, pageName] = page;
    content.value = <MarkdownContent filename={pageName!} section={section} />;
  }
});

export function App() {
  const isOpen = useSignal(false);

  return (
    <>
      <Navbar
        requestMenu={() => {
          isOpen.value = true;
        }}
      />
      <OffCanvas
        isOpen={isOpen}
        onClose={() => {
          isOpen.value = false;
        }}
      />
      <main>{content.value}</main>
      <footer></footer>
    </>
  );
}
