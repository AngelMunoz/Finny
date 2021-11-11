//@ts-ignore
import { SlButton } from "@shoelace-style/shoelace/dist/react/index.js";

function FeatureListItem({ header, content }: FeatureListItemProps) {
  return (
    <li className="features__feature-list-item">
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
      content:
        "Using .NET and esbuild under the hood means that this is blazing fast!",
    },
    {
      header: "Cross-Platform",
      content: "Windows, Linux, MacOS, even on your Raspberry Pi!",
    },
  ],
  [
    {
      header: "Typescript or JSX/TSX?",
      content:
        "From modern Javascript to Typescript, feel free to develop in your flavor of choice.",
    },
    {
      header: "Single binary or .NET Tool",
      content:
        "Download the executable or if you're a .NET user just install the tool.",
    },
  ],
  [
    {
      header: "No Local dependencies",
      content: (
        <>
          Forget about webpack, npm, and other complex tooling, Perla uses CDN's
          like:
          <ul>
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
              <SlButton
                type="text"
                target="_blank"
                href="https://jspm.org/docs/cdn"
              >
                JSPM
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
  ],
];

export function Index() {
  return (
    <article className="page index-page">
      <header>
        <h1>Welcome to Perla!</h1>
      </header>

      <p>
        Perla is a take modern, fast and simple to use JS tooling that doesn't
        require node.js
      </p>

      <section className="action-buttons">
        <SlButton type="primary" outline href="#/content/index">
          Get Started
        </SlButton>
        <SlButton type="primary" outline href="/#/content/learn">
          Learn More
        </SlButton>
      </section>

      <section className="features">
        <header>
          <h3>Features</h3>
        </header>
        {rows.map((row) => (
          <FeatureList features={row} />
        ))}
      </section>
    </article>
  );
}
