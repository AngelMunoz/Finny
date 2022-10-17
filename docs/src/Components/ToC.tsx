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

export function ToC({ isAside }: { isAside?: boolean }) {
  return isAside ? (
    <aside className="markdown-aside">
      {Object.entries(toc).map(([, section]) => getEntryTpl(section))}
    </aside>
  ) : (
    <>{Object.entries(toc).map(([, section]) => getEntryTpl(section))}</>
  );
}
