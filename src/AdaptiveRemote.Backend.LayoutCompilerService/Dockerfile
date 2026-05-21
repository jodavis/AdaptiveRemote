FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY ["src/AdaptiveRemote.Contracts/AdaptiveRemote.Contracts.csproj", "AdaptiveRemote.Contracts/"]
COPY ["src/AdaptiveRemote.Backend.Common/AdaptiveRemote.Backend.Common.csproj", "AdaptiveRemote.Backend.Common/"]
COPY ["src/AdaptiveRemote.Backend.LayoutCompilerService/AdaptiveRemote.Backend.LayoutCompilerService.csproj", "AdaptiveRemote.Backend.LayoutCompilerService/"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
RUN dotnet restore "AdaptiveRemote.Backend.LayoutCompilerService/AdaptiveRemote.Backend.LayoutCompilerService.csproj"

# Copy source and publish (Native AOT for linux-x64)
COPY ["src/AdaptiveRemote.Contracts/", "AdaptiveRemote.Contracts/"]
COPY ["src/AdaptiveRemote.Backend.Common/", "AdaptiveRemote.Backend.Common/"]
COPY ["src/AdaptiveRemote.Backend.LayoutCompilerService/", "AdaptiveRemote.Backend.LayoutCompilerService/"]
WORKDIR "/src/AdaptiveRemote.Backend.LayoutCompilerService"
RUN dotnet publish "AdaptiveRemote.Backend.LayoutCompilerService.csproj" -c Release -r linux-x64 -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["./bootstrap"]
