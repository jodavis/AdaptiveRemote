FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY ["src/AdaptiveRemote.Contracts/AdaptiveRemote.Contracts.csproj", "AdaptiveRemote.Contracts/"]
COPY ["src/AdaptiveRemote.Backend.RawLayoutService/AdaptiveRemote.Backend.RawLayoutService.csproj", "AdaptiveRemote.Backend.RawLayoutService/"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
RUN dotnet restore "AdaptiveRemote.Backend.RawLayoutService/AdaptiveRemote.Backend.RawLayoutService.csproj"

# Copy source and build
COPY ["src/AdaptiveRemote.Contracts/", "AdaptiveRemote.Contracts/"]
COPY ["src/AdaptiveRemote.Backend.RawLayoutService/", "AdaptiveRemote.Backend.RawLayoutService/"]
WORKDIR "/src/AdaptiveRemote.Backend.RawLayoutService"
RUN dotnet build "AdaptiveRemote.Backend.RawLayoutService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AdaptiveRemote.Backend.RawLayoutService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "AdaptiveRemote.Backend.RawLayoutService.dll"]
