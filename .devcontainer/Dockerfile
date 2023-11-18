FROM mcr.microsoft.com/devcontainers/base:ubuntu-22.04

RUN curl -fsSL https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.sh | bash /dev/stdin -c 8.0 --install-dir /home/vscode/.dotnet

ENV DOTNET_ROOT=/home/vscode/.dotnet
ENV PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools

RUN chown -R vscode:vscode $DOTNET_ROOT
