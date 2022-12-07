type DocsVersion = `v${number}`;

type ContentKind = "Content" | "Docs" | "Blogs";

type MarkdownContentProps = {
  filename: string;
  section?: string;
  version?: DocsVersion;
  contentKind?: ContentKind;
};

type OffCanvasProps = {
  isOpen?: boolean;
  onClose?(): void;
};

type NavbarProps = {
  requestMenu?(): void;
};

type ToCProps = {
  isAside?: boolean;
  version?: DocsVersion;
};

type FeatureListItemProps = {
  header: string;
  content: string | JSX.Element;
};

type FeatureListProps = {
  features: FeatureListItemProps[];
};

type Page =
  | ["Home"]
  | ["Blogs"]
  | [ContentKind, DocsVersion, string | undefined, string];

type ToCLink = {
  title: string;
  url: string;
};

type ToCSection = {
  label: string;
  sections: ToCLink[];
  open?: boolean;
};

type TableOfContents = {
  GettingStarted: ToCSection;
  [key: DocsVersion]: Record<string, ToCSection>;
};

type Blog = {
  title: string;
  summary: string;
  date: string;
  url: string;
};

// NAVIGO TYPES
type Match = {
  url: string;
  queryString: string;
  hashString: string;
  route: Route;
  data: Record<string, unknown> | null;
  params: Record<string, string> | null;
};

type RouteHooks = {
  before?: (done: Function, match: Match) => void;
  after?: (match: Match) => void;
  leave?: (done: Function, match: Match | Match[]) => void;
  already?: (match: Match) => void;
};

type Route = {
  name: string;
  path: string | RegExp;
  handler: Function;
  hooks: RouteHooks;
};

// json modules

declare module "*toc.json?module" {
  var toc: TableOfContents;
  export default toc;
}

declare module "*blogs.json?module" {
  var blogs: Blog[];
  export default blogs;
}
