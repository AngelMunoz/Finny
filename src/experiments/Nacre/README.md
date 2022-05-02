# Nacre

First setup playwright on your machine

- If you have node installed
  - `npx playwright install`
- If you have dotnet installed
  - `dotnet tool install --global Microsoft.Playwright.CLI`
  - `cd Nacre`
  - `dotnet build`
  - `playwright install`
  - `cd ../`

Then you can run:

```
dotnet run --project Nacre --all-files true --root-directory ../samples/sample-project
```
