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
    | ['Content', string | undefined, string]
    | ['Docs', string | undefined, string];
