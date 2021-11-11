//@ts-ignore
import Navigo from 'navigo';

//@ts-ignore
import { BehaviorSubject } from 'rxjs';

export const Router = new Navigo("/", {
    hash: true,
    linksSelector: "a"
});

export const Page = new BehaviorSubject<Page>('Home');

Router
    .on("", () => Page.next('Home'))
    .on("content/:filename", ({ data }: { data?: MarkdownContentProps; }) => {
        if (!data?.filename) return;
        Page.next(['Content', data.section, data.filename]);
    })
    .on(
        "docs/:section/:filename",
        ({ data }: { data?: MarkdownContentProps; }) => {
            if (!data?.filename) return;
            Page.next(['Docs', data.section, data.filename]);
        }
    )
    .on("blog/:filename", ({ data }: { data?: MarkdownContentProps; }) => {
        if (!data?.filename) return;
        Page.next(['Blog', data.section, data.filename]);
    })
    .notFound(() => {
        Page.next(['Blog', undefined, "not-found"]);
    });

Router.resolve();
