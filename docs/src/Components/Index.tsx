//@ts-ignore
import { SlButton } from "@shoelace-style/shoelace/dist/react/index.js";

function FeatureListItem({ header, content }: FeatureListItemProps) {
  return (
    <li className="features__feature-list-item card with-box-shadow">
      <h3>{header}</h3>
      <p>{content}</p>
    </li>
  );
}

function FeatureList({ features }: FeatureListProps) {
  return (
    <ul className="features__feature-list">
      {features.map((feature) => (
        <FeatureListItem {...feature} />
      ))}
    </ul>
  );
}

const rows = [
  [
    {
      header: "Fast",
      content: (
        <>
          <p>
            Perla uses <b>.NET</b> and <b>Go</b> under the hood for a performant
            development experience!
          </p>
        </>
      ),
    },
    {
      header: "Cross-Platform",
      content: "Windows, Linux, MacOS, even on your Raspberry Pi!",
    },
    {
      header: "Single binary or .NET Tool",
      content:
        "Download the tool or if you're a .NET user just install the dotnet tool.",
    },
    {
      header: "Typescript or JSX/TSX?",
      content:
        "From modern Javascript to Typescript, develop in your flavor of choice.",
    },
    {
      header: "No Local dependencies",
      content: (
        <>
          Forget about webpack, npm, and other complex tooling, Perla uses CDNs
          like:
          <ul className="link-list">
            <li>
              <SlButton
                type="text"
                target="_blank"
                href="https://jspm.org/docs/cdn"
              >
                JSPM
              </SlButton>
            </li>
            <li>
              <SlButton
                type="text"
                target="_blank"
                href="https://www.skypack.dev/"
              >
                Skypack
              </SlButton>
            </li>
            <li>
              <SlButton type="text" target="_blank" href="https://unpkg.com/">
                Unpkg
              </SlButton>
            </li>
          </ul>
        </>
      ),
    },
    {
      header: "Unbundled Development",
      content:
        "Let the browser help you, reduce feedback loops between your code changes with unbundled development!",
    },
  ],
];

export function Index() {
  return (
    <article className="page index-page">
      <header className="index-header">
        <h1>Welcome to Perla!</h1>
        <p>Perla is a take on modern tooling for front-end development.</p>
        <p>
          A fast, and simple to use development server. No node.js knowledge
          required.
        </p>
      </header>

      <section className="action-buttons">
        <SlButton type="primary" size="large" outline href="#/content/install">
          Get Started
        </SlButton>
        <SlButton type="primary" size="large" outline href="/#/content/index">
          Learn More
        </SlButton>
      </section>

      <section className="features">
        <header className="index-header">
          <h3>Features</h3>
        </header>
        {rows.map((row) => (
          <FeatureList features={row} />
        ))}
      </section>
    </article>
  );
}
