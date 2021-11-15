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
