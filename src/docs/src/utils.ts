
export function buildUrl(filename: string, section?: string) {
    let name = filename.endsWith(".md") ? filename : `${filename}.md`;
    let url = "/assets/docs";
    if (section) {
        url += `/${section}`;
    }
    url += `/${name}`;
    return url;
}
