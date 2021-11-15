## Requirements

- [.NET6](https://dotnet.microsoft.com/download/dotnet/6.0)

## Editor

You can use any editor of your choice

- [Ionide](https://marketplace.visualstudio.com/items?itemName=Ionide.Ionide-fsharp) (recommended as it is the simplest setup possible)
- [Rider](https://www.jetbrains.com/rider/)
- [Visual Studio](https://visualstudio.microsoft.com/vs/community/)
  - Use the [F# formatting extension](https://marketplace.visualstudio.com/items?itemName=asti.fantomas-vs)

Both rider and ionide pick up fantomas's configuration when formating/format on saving a file visual studio requires an extra plugin. If you use any other tool like fvim or variants please just don't forget to format your files before pushing them via the local fantomas tool.

## Build

The project is a .NET console app so you don't need to do anything special, restore local tool and then build the project with the dotnet cli/IDE options

```
dotnet tool restore
dotnet build
```

If you want to build your own packages for testing just run at the root of this repository

```
dotnet fsi ./build.fsx
```

that will generate every binary file, zip them and the NuGet package, if you're only interested in the NuGet package

```
dotnet fsi ./build.fsx -t PackNugets
```

## Manual Testing

If you want to test your changes manually (preferred way as the time of writing due to lack of tests) you can use the App project

```sh
cd src/App
dotnet run --project ../Perla -- serve # or any other command
```

While the main focus is .NET6 please ensure your changes work in .NET5 untill we drop support for it.
For VSCode/Visual Studio there are some Debug configurations that help on this scenario, if the configuration is generic enough, feel free to include it in your PR however if it's a different version of an existing configuration please don't include it.

## Tests

There are no tests as the time of writing however if you want to setup tests and start with simple unit tests to ensure your changes work, by al means feel free to include them.

## Code

Please ensure your code is Formatted with Fantomas and that you're roughly following the [F# Style Guide](https://docs.microsoft.com/en-us/dotnet/fsharp/style-guide/) (which fantomas helps you follow).

You can send PR's without issues if you think the problem/fix is relatively simple/short, however if you want to pursue a more complex feature, please open an Issue or mark your PR as a draft so we can discuss the changes and potential impact on the code.

Please keep in mind the following:

- **Sending a PR doesn't mean it will get merged**
- It might be required from your part to do few or several changes before getting a PR merged
- Your PR changes might get edited, modified by the maintainers before/after a possible merge

## Documentation

If you want to update content or edit typos simply look for `src/docs/assets/**/Your Markdown File.md` edit and send the PR right away, no need to download the project at all.
If you want to help with the docs website in any way, be it CSS or adding routes or other components then you want to keep reading.

The documentation is a dogfooding project under the directory of `src/docs` it is a react website, if you're using VSCode there is a background task you can run which is the `docs` task this will spin up Perla under the docs directory and you should be able to open `localhost:7331`. If you're not using VSCode then you will need to do the following

```
cd src/docs
dotnet run --project ../Perla -- serve
```

This will spin up Perla on the docs website and youll be able to see it in `localhost:7331`
