[.net tool]: https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools

# Installation

Perla provides two ways for installation

- For most people, you only need to download the corresponding zip file for your platform and ensure it is on your path, this process will be simplified once we have a stable release.
- If you're a .NET user you can install Perla as a global or local [.NET Tool].

# Binary Files

1. Go to https://github.com/AngelMunoz/Perla/releases
2. Pick your release and from the release Assets download your platform zip file (example: `win10-x64.zip` or `osx-x64.zip`)
3. Uncompress the zip to the directory you want to keep perla on (example: `$HOME/Apps/perla` or `C:\Users\User\Apps\perla`)
4. Ensure Perla is on the system's `PATH`
   - Linux
     - on your `~/.bashrc` append the following
     ```sh
     # replace $HOME/Apps/perla with your chosen location
     export PERLA_PATH=$HOME/Apps/perla
     export PATH=PERLA_PATH:$PATH
     ```
     - Log out and Log in again for it to make effect
   - MacOS
     - on your `~/.zshrc` append the following
     ```sh
     # replace $HOME/Apps/perla with your chosen location
     export PERLA_PATH=$HOME/Apps/perla
     export PATH=PERLA_PATH:$PATH
     ```
     - Log out and Log in again for it to make effect
   - Windows
     - Press <kbd>Win</kbd>+<kbd>R</kbd>
     - Type `SystemPropertiesAdvanced.exe` and press enter
     - Press the `Environment Variables` button
     - Update the `PATH` variable with the location of your `Perla.exe` file
     - Log out and Log in again for it to make effect

# .NET Users

The easiest way to install Perla is using .NET since we provide it as a .NET tool

To install it as a tool:

- As a global tool:
  ```sh
  dotnet tool install --global Perla
  ```
- As a local tool:
  ```sh
  dotnet new tool-manifest # if you are setting up for the first time
  dotnet tool install --local Perla
  ```
