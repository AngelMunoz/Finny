//@ts-ignore
import Navigo from "navigo";

//@ts-ignore
import { BehaviorSubject } from "rxjs";

export const Router = new Navigo("/", {
  hash: true,
  linksSelector: "a",
});

export const Page: {
  next(page: Page): void;
  subscribe(onNext: (page: Page) => void): () => void;
} = new BehaviorSubject<Page>("Home");

const setHeaderPosition = ({ params }: Match) => {
  const id = params?.id;
  const el = document.querySelector(`#${id}`);
  el?.scrollIntoView(true);
};

Router.hooks({
  after: setHeaderPosition,
  already: setHeaderPosition,
});

Router.on("", () => Page.next(["Home"]))
  .on("/content/:filename", ({ data }: { data?: MarkdownContentProps }) => {
    if (!data?.filename) return;
    Page.next(["Content", data.version ?? "v1", data.section, data.filename]);
  })
  .on(
    ":version/docs/:section/:filename",
    ({ data }: { data?: MarkdownContentProps }) => {
      if (!data?.filename) return;
      Page.next(["Docs", data?.version ?? "v1", data.section, data.filename]);
    }
  )
  .on("blogs/:filename", ({ data }: { data?: MarkdownContentProps }) => {
    if (!data?.filename) return;
    Page.next(["Blogs", data.version ?? "v1", data.section, data.filename]);
  })
  .on("blogs", () => {
    Page.next(["Blogs"]);
  })
  .notFound(() => {
    Page.next(["Blogs", "v1", undefined, "not-found"]);
  });

Router.resolve();

setTimeout(() => {
  const location: Match = Router.getCurrentLocation();
  setHeaderPosition(location);
}, 1000);
