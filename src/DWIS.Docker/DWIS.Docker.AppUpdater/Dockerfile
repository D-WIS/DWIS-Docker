# Build context: src/DWIS.Docker
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["DWIS.Docker.Core/DWIS.Docker.Core.csproj", "DWIS.Docker.Core/"]
COPY ["DWIS.Docker.AppUpdater/DWIS.Docker.AppUpdater.csproj", "DWIS.Docker.AppUpdater/"]
RUN dotnet restore "DWIS.Docker.AppUpdater/DWIS.Docker.AppUpdater.csproj"
COPY DWIS.Docker.Core/ DWIS.Docker.Core/
COPY DWIS.Docker.AppUpdater/ DWIS.Docker.AppUpdater/
RUN dotnet publish "DWIS.Docker.AppUpdater/DWIS.Docker.AppUpdater.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DWIS.Docker.AppUpdater.dll"]
