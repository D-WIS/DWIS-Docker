# Build context: src/DWIS.Docker
# docker build -f DWIS.Docker.Web/Dockerfile -t digiwells/dwis-docker:stable .

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
# The app talks to the mounted Docker socket; run as root so the socket is accessible on every host.
USER root
ENV ASPNETCORE_HTTP_PORTS=8080
ENV DWIS_DATA_DIR=/home/dwis-docker

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release

# Corporate root CAs so NuGet, Docker Hub and the hub are reachable behind the NORCE firewall.
COPY NORCE_CA.cer /usr/local/share/ca-certificates/norce.crt
COPY hub.docker.cer /usr/local/share/ca-certificates/dockerhub.crt
RUN chmod 644 /usr/local/share/ca-certificates/*.crt && update-ca-certificates

WORKDIR /src
COPY ["DWIS.Docker.Core/DWIS.Docker.Core.csproj", "DWIS.Docker.Core/"]
COPY ["DWIS.Docker.UI/DWIS.Docker.UI.csproj", "DWIS.Docker.UI/"]
COPY ["DWIS.Docker.Web/DWIS.Docker.Web.csproj", "DWIS.Docker.Web/"]
RUN dotnet restore "DWIS.Docker.Web/DWIS.Docker.Web.csproj"
COPY DWIS.Docker.Core/ DWIS.Docker.Core/
COPY DWIS.Docker.UI/ DWIS.Docker.UI/
COPY DWIS.Docker.Web/ DWIS.Docker.Web/
RUN dotnet publish "DWIS.Docker.Web/DWIS.Docker.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DWIS.Docker.Web.dll"]
