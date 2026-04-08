FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY ["src/AdaptiveRemote.Contracts/AdaptiveRemote.Contracts.csproj", "AdaptiveRemote.Contracts/"]
COPY ["src/AdaptiveRemote.Backend.CompiledLayoutService/AdaptiveRemote.Backend.CompiledLayoutService.csproj", "AdaptiveRemote.Backend.CompiledLayoutService/"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
RUN dotnet restore "AdaptiveRemote.Backend.CompiledLayoutService/AdaptiveRemote.Backend.CompiledLayoutService.csproj"

# Copy source and build
COPY ["src/AdaptiveRemote.Contracts/", "AdaptiveRemote.Contracts/"]
COPY ["src/AdaptiveRemote.Backend.CompiledLayoutService/", "AdaptiveRemote.Backend.CompiledLayoutService/"]
WORKDIR "/src/AdaptiveRemote.Backend.CompiledLayoutService"
RUN dotnet build "AdaptiveRemote.Backend.CompiledLayoutService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AdaptiveRemote.Backend.CompiledLayoutService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "AdaptiveRemote.Backend.CompiledLayoutService.dll"]
