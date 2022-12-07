import "./App.css";
//@ts-ignore
import { useSignal, signal } from "@preact/signals";
import { Page } from "./router.js";
import { Index } from "./Components/Index.js";
import { Sidenav } from "./Components/Sidenav.js";
import { MarkdownContent } from "./Components/MarkdownContent.js";
import { BlogList } from "./Components/BlogList.js";
import Blogs from "./blogs.json?module";
import Toc from "./toc.json?module";

const route = signal("Home");

const gettingStarted = Toc["GettingStarted"];

const versions =
  Object.entries(Toc)
    .reduce((current, [key, content]) => {
      if (key === "GettingStarted") {
        return current;
      }
      const versioned = {
        [key]: /** @type {Record<string, ToCSection>} */ (content),
      };
      current.push(versioned);
      return current;
    }, /** @type {Record<String, Record<string, ToCSection>>[]} */ ([]))
    ?.reverse?.() ?? [];

const sidenav = (
  <Sidenav
    hidden={route.value === "Home"}
    gettingStarted={gettingStarted}
    versions={versions}
  />
);

/**
 *
 * @param {OffCanvasProps} props
 * @returns
 */
function OffCanvas({ isOpen, onClose }) {
  return (
    //@ts-ignore
    <sl-drawer
      label="Table of Contents"
      open={isOpen}
      placement="start"
      onsl-after-hide={() => (console.log("dude"), onClose?.())}
    >
      <div className="off-canvas-sidenav">{sidenav}</div>
      {onClose ? (
        <sl-button slot="footer" variant="primary" onClick={() => onClose()}>
          Close
        </sl-button>
      ) : null}
    </sl-drawer>
  );
}

/**
 *
 * @param {NavbarProps} param0
 * @returns
 */
function Navbar({ requestMenu }) {
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
          <sl-button href={`/#/`} variant="text" size="large">
            Perla
          </sl-button>
        </section>
        <section className="nav-links">
          <ul className="link-list">
            <li>
              <sl-button href={"/#/content/index"} variant="text">
                Docs
              </sl-button>
            </li>
            <li>
              <sl-button href="/#/v0/docs/features/development" variant="text">
                V0 Docs
              </sl-button>
            </li>
            <li>
              <sl-button href="/#/blogs" variant="text">
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

/**
 * @type {{ value: DocsVersion }}
 */
const version = signal("v1");
const content = signal(<Index />);

Page.subscribe(
  /**
   *
   * @param {Page} page
   */
  ([page, ver, section, pageName]) => {
    route.value = page;
    if (page === "Home") {
      content.value = <Index />;
    } else if (page === "Blogs") {
      content.value = <BlogList blogs={Blogs} />;
    } else {
      version.value = ver;
      content.value = (
        <MarkdownContent
          version={ver}
          filename={pageName}
          section={section}
          contentKind={page}
        />
      );
    }
  }
);

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
      <main className={`${route.value}`}>
        {sidenav}
        {content.value}
      </main>
      <footer></footer>
    </>
  );
}
