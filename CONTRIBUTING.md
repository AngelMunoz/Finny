## Requirements

- [.NET5](https://dotnet.microsoft.com/download/dotnet/5.0)
- [.NET6](https://dotnet.microsoft.com/download/dotnet/6.0)

## Editor
You can use any editor of your choice

- [Ionide](https://marketplace.visualstudio.com/items?itemName=Ionide.Ionide-fsharp) (recommended)
- [Rider](https://www.jetbrains.com/rider/)
- [Visual Studio](https://visualstudio.microsoft.com/vs/community/)
  - If using 2019 use the [F# formatting extension](https://marketplace.visualstudio.com/items?itemName=asti.fantomas-vs)
  - If using 2022 please format your files running `dotnet fantomas src/Perla/MyFile.fs`

Both rider and ionide pick up fantomas's configuration when formating/format on saving a file visual studio requires an extra plugin. If you use any other tool like fvim or variants please just don't forget to format your files before pushing them via the local fantomas tool.


## Build
The project is a .NET console app so you don't need to do anything special, restore local tool and then build the project with the dotnet cli/IDE options
```
dotnet tool restore
dotnet build
```

## Manual Testing
If you want to test your changes manually (preferred way as the time of writing due to lack of tests) you can use the App project

```sh
cd src/App
dotnet run --project ../Perla -f net6.0 -- serve # or any other command 
# or dotnet run --project ../Perla -f net5.0 -- serve # or any other command 
```
While the main focus is .NET6 please ensure your changes work in .NET5 untill we drop support for it.
For VSCode/Visual Studio there are some Debug configurations that help on this scenario, if the configuration is generic enough, feel free to include it in your PR however if it's a different version of an existing configuration please don't include it.


## Tests
There are no tests as the time of writing however if you want to setup tests and start with simple unit tests to ensure your changes work, by al means feel free to include them.

## Code
You can send PR's without issues if you think the problem/fix is relatively simple/short, however if you want to pursue a more complex feature, please open an Issue or mark your PR as a draft so we can discuss the changes and potential impact on the code.

Please keep in mind the following:

- **Sending a PR doesn't mean it will get merged**
- It might be required from your part to do few or several changes before getting a PR merged
- Your PR changes might get edited, modified by the maintainers before/after a possible merge


