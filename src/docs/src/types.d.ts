type MarkdownContentProps = {
    filename: string;
    section?: string;
};

type FeatureListItemProps = {
    header: string;
    content: string | JSX.Element;
};

type FeatureListProps = {
    features: FeatureListItemProps[];
};


type Page =
    | 'Home'
    | ['Content' | 'Docs' | 'Blog',
        string | undefined,
        string];

type ToCLink = {
    title: string;
    url: string;
};

type ToCSection = {
    label: string;
    sections: ToCLink[];
};

type ToC = {
    Features: ToCSection;
    GettingStarted: ToCSection;
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
