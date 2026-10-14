# DWIS-Docker — Getting Started

DWIS-Docker is a container management tool for D-WIS services. It runs as a Docker container and exposes a web UI to manage the other D-WIS containers on the same host: blackboards, the scheduler, the microstate engine, the advice composer, the ADCS bridges, client web apps and advisor services. A Windows desktop version with the same UI is also available.

> **Note:** This application is a work in progress. Use it with that in mind and feel free to report any issues.

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running

## 1. Launch the app

Create a folder named `C:\Volumes` on your machine:

```powershell
mkdir C:\Volumes
```

> You can use a different path, but make sure to update the `-v C:\Volumes:/home` part of the command below accordingly. The app detects the folder you mounted and uses it for the module configuration folders and its own settings.

Run the following command to pull and start the container:

```bash
docker run --name dwis-docker --user root -P \
  -p 5272:8080/tcp \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v C:\Volumes:/home \
  -dit digiwells/dwis-docker:stable
```

Then open your browser and navigate to:

```
http://localhost:5272
```

What ends up in `C:\Volumes`:

- `dwis-docker\settings.json` — the app settings (Docker endpoint, hubs, replication target, …). They survive updates of the app.
- `<Module>\config.json` — one folder per module that has a configuration, for example `DWISAdviceComposerService\config.json`. The folder is mounted into the module container as `/home`.
- `Recordings\<name>\` — one folder per recording made from the Recording tab (chunk files plus an `rtData` index).

## 2. The tabs

- **Replication** — the SignalR hubs the blackboards can replicate to. Add or remove hubs, connect, see the groups and how many blackboards are connected to each, create groups, set a group write password, clear a group's manifests, and pick the hub and group the standard setup replicates to. You can also **launch a hub locally** (a `digiwells/dwissignalr` container on a port of your choice, optionally pulling data from or pushing data to groups of other hubs) and start **hub-to-hub replications** (a standalone container that copies one hub group to another).
- **Standard setup** — every predefined container in one table, grouped by category. Each row shows whether the container exists, whether it runs, whether the image is up to date, and whether its configuration file exists. Select rows for bulk actions (create missing, start, stop, restart, update images, delete).
- **Blackboards** — all blackboard containers on the engine, with their replication hub, group and OPC UA port. Create extra blackboards here.
- **Recording** — record what a hub group streams into a folder under `C:\Volumes\Recordings\<name>`, and replay a recording into a hub group later. Each recording shows its duration, size and whether a recorder or player container is running. The recorder and player images are set in the settings.
- **DWIS modules** — scheduler, microstates, composer and the ADCS bridges, each with an inline editor for its `config.json`.
- **Clients** and **Advisors** — the web applications and advisor services, with a link to each app's port.

The bottom bar shows the Docker endpoint in use and the replication target. The gear opens the settings (Docker API endpoint, host volumes root, blackboard host IP, polling interval). **Update application** pulls the latest `dwis-docker` image and recreates this container.

## 3. First test

This test launches a **Blackboard** that replicates to the **Blackboard Hub** hosted by NORCE. Real-time data streams in that hub, which makes it a good environment for early testing of any D-WIS installation.

1. Open the **Replication** tab. The NORCE hub is preconfigured and connects automatically. Make sure **Replicate** is on, the hub is **NORCE** and the group is `default`, then **Save** if you changed anything.
2. Open the **Standard setup** tab. In the **Blackboards** group, click the **create** button of the *Default Blackboard* row. The image is pulled if needed and the container is created and started. Give it a minute to warm up. The row now shows *Running* and `NORCE / default` on port 48030.
3. Back on the **Replication** tab, the `default` group now lists your blackboard among its connections.
4. Open the **Clients** tab and create **Log view** and **Blackboard explorer**. Docker images will be downloaded, which can take a few minutes.
5. Click the port links (`localhost:5276` and `localhost:5277`) to open the applications:
   - **Log view**: some standard drilling logs
   - **Blackboard explorer**: a simple view of the contents of the blackboard

## 4. Test OPC-UA connection

You can connect any OPC-UA client to the blackboard. By default, a no-security connection is available at:

```
opc.tcp://localhost:48030
```

Once connected, you can test subscribing to streaming data.

## 5. Desktop version (Windows)

The same UI is available as a Windows application that talks to the local Docker Desktop directly. Build and run it from the source tree:

```bash
dotnet run --project src/DWIS.Docker/DWIS.Docker.Desktop
```

Settings are stored in `%LOCALAPPDATA%\DWIS.Docker\settings.json` and the module folders in `C:\Volumes` (configurable in the settings).

## Building from source

```bash
dotnet build src/DWIS.Docker/DWIS.Docker.sln
dotnet test src/DWIS.Docker/DWIS.Docker.Core.Tests
dotnet run --project src/DWIS.Docker/DWIS.Docker.Web --urls http://localhost:5272
```

Docker images (run from `src/DWIS.Docker`):

```bash
docker build -f DWIS.Docker.Web/Dockerfile -t digiwells/dwis-docker:stable .
docker build -f DWIS.Docker.AppUpdater/Dockerfile -t digiwells/dwis-app-update:stable .
```
