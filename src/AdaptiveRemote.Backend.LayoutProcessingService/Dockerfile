FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY ["src/AdaptiveRemote.Contracts/AdaptiveRemote.Contracts.csproj", "AdaptiveRemote.Contracts/"]
COPY ["src/AdaptiveRemote.Backend.LayoutProcessingService/AdaptiveRemote.Backend.LayoutProcessingService.csproj", "AdaptiveRemote.Backend.LayoutProcessingService/"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
RUN dotnet restore "AdaptiveRemote.Backend.LayoutProcessingService/AdaptiveRemote.Backend.LayoutProcessingService.csproj"

# Copy source and build
COPY ["src/AdaptiveRemote.Contracts/", "AdaptiveRemote.Contracts/"]
COPY ["src/AdaptiveRemote.Backend.LayoutProcessingService/", "AdaptiveRemote.Backend.LayoutProcessingService/"]
WORKDIR "/src/AdaptiveRemote.Backend.LayoutProcessingService"
RUN dotnet build "AdaptiveRemote.Backend.LayoutProcessingService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AdaptiveRemote.Backend.LayoutProcessingService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "AdaptiveRemote.Backend.LayoutProcessingService.dll"]
