export function buildUrl(filename: string, section?: string) {
  let name = filename.endsWith(".html") ? filename : `${filename}.html`;
  let url = "/assets/docs";
  if (section) {
    url += `/${section}`;
  }
  url += `/${name}`;
  return url;
}
