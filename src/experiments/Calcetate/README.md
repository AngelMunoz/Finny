# How to Run

To run this PoC first produce a nuget package from the `CalceTypes\CalceTypes.fsproj` project

```
cd CalceTypes
dotnet pack -o nupkg
```

then update the plugin file `Calcetate\Plugin.fsx` local sources directive

- `#i "nuget: C:\\absolute\\path\\to\\your-project\\CalceTypes\\nupkg"`
- `#i "nuget: /absolute/path/to/your-project/CalceTypes/nupkg"`

after that you should be able to run the main `Calcetate\Calcetate.fsproj` project

```
cd Calcetate
dotnet run
```
