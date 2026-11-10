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

> **Hub with a company certificate?** If one of your hubs is served over HTTPS with a certificate issued by a company CA, the command above needs one more line. See [Hubs with a certificate from a company CA](#hubs-with-a-certificate-from-a-company-ca) below.

What ends up in `C:\Volumes`:

- `dwis-docker\settings.json` — the app settings (Docker endpoint, hubs, replication target, …). They survive updates of the app. This folder is also the place for the certificate file of a hub with a company certificate (see below).
- `<Module>\config.json` — one folder per module that has a configuration, for example `DWISAdviceComposerService\config.json`. The folder is mounted into the module container as `/home`.
- `Recordings\<name>\` — one folder per recording made from the Recording tab (chunk files plus an `rtData` index).

### Hubs with a certificate from a company CA

When a hub is served over HTTPS with a certificate issued by a private CA (for example an internal corporate CA), connections to it fail with a certificate chain error, even when Windows itself trusts that CA. The app and the containers it launches are Linux containers, and each of them has its own list of trusted CAs. So the certificate must be given in two places: to the app itself (for its own connection to the hub in the Replication tab) and to the containers the app launches (blackboards, recorders, players, hubs).

1. **Get the certificate as a PEM file.** Either export the root CA from the Windows certificate store as *Base-64 encoded X.509 (.CER)* and rename the file to `.pem`, or use the server's full chain. The file holds public certificates only, never a private key.
2. **Copy the file into the app's folder**, for example `C:\Volumes\dwis-docker\company-ca.pem`. The app sees this folder as `/home/dwis-docker`.
3. **Start the app with `SSL_CERT_FILE` pointing at the file.** This is the command above with one more line (the `-e` line):

   ```bash
   docker run --name dwis-docker --user root -P \
     -p 5272:8080/tcp \
     -v /var/run/docker.sock:/var/run/docker.sock \
     -v C:\Volumes:/home \
     -e SSL_CERT_FILE=/home/dwis-docker/company-ca.pem \
     -dit digiwells/dwis-docker:stable
   ```

   If the `dwis-docker` container already exists, remove it first with `docker rm -f dwis-docker`: a variable cannot be added to an existing container. The settings are not lost, they are in `C:\Volumes\dwis-docker`. **Update application** keeps the variable.
4. **Set the *Extra CA certificate file* setting** (gear icon) to the path of the same file **on the host**, here `C:\Volumes\dwis-docker\company-ca.pem`. Every container the app creates that connects to a hub then gets the file mounted as `/certs/extra-ca.pem` with `SSL_CERT_FILE` pointing at it. Containers created before the setting was changed do not have it: delete and create them again, or use **update image**.

The certificates in the file are trusted in addition to the public CAs. Hubs with a public certificate need none of this: use the plain command and leave the setting empty. The Windows desktop version (section 5) uses the Windows certificate store for its own connection, so only steps 1, 2 and 4 apply to it.

### Running on a remote machine

The app manages the Docker engine it is connected to, and the module configuration folders must be on the same machine as that engine. So if your Docker engine runs on a remote machine (for example a Linux VM you reach over SSH), run the app **on that machine** and open its web UI from your PC through an SSH port forward.

On the remote machine, create the volumes folder and start the container with the engine's own socket:

```bash
mkdir -p ~/Volumes
docker run --name dwis-docker --user root -P \
  -p 5272:8080/tcp \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v ~/Volumes:/home \
  -dit digiwells/dwis-docker:stable
```

On your PC, forward the web port over SSH (replace `user@remote-host` with your login and the machine's address) and keep this command running:

```bash
ssh -N -L 5272:localhost:5272 user@remote-host
```

Then open your browser and navigate to `http://localhost:5272` as usual. The web UI, the engine, the module folders and the bind mounts all stay on the remote machine, so everything works as on a local installation.

A few things to know about this setup:

- The port 5272 is only reachable through the SSH tunnel. Nothing else needs to be opened on the remote machine's firewall.
- Links to module web apps (Log view, Blackboard explorer, …) point to `localhost:<port>`. To open them from your PC, forward their ports too, for example `-L 5276:localhost:5276 -L 5277:localhost:5277` on the same `ssh` command.
- The OPC-UA endpoint of a blackboard (`opc.tcp://localhost:48030`) can be forwarded the same way with `-L 48030:localhost:48030`.
- On a Linux host, the **Blackboard host IP** setting (default `host.docker.internal`) may have to be changed to the host's IP on the Docker bridge, usually `172.17.0.1`, so that modules can reach the blackboard.
- For a hub with a company certificate, follow [the steps above](#hubs-with-a-certificate-from-a-company-ca) with the file in `~/Volumes/dwis-docker/`. The extra `-e SSL_CERT_FILE=/home/dwis-docker/company-ca.pem` line is the same, and the *Extra CA certificate file* setting is the full path on the remote machine, for example `/home/<user>/Volumes/dwis-docker/company-ca.pem`.
- Do not expose the Docker API itself (port 2375) to the network as a shortcut. The app has no TLS settings, and an open Docker API gives root access to the machine.

## 2. The tabs

- **Replication** — the SignalR hubs the blackboards can replicate to. Add or remove hubs, connect, see the groups and how many blackboards are connected to each, create groups, set a group write password, clear a group's manifests, and pick the hub and group the standard setup replicates to. You can also **launch a hub locally** (a `digiwells/dwissignalr` container on a port of your choice, optionally pulling data from or pushing data to groups of other hubs) and start **hub-to-hub replications** (a standalone container that copies one hub group to another).
- **Standard setup** — every predefined container in one table, grouped by category. Each row shows whether the container exists, whether it runs, whether the image is up to date, and whether its configuration file exists. Select rows for bulk actions (create missing, start, stop, restart, update images, delete).
- **Blackboards** — all blackboard containers on the engine, with their replication hub, group and OPC UA port. Create extra blackboards here. A port that is already in use does not block the creation: the form shows a warning, and if the port is still taken when the blackboard should start, the container is created but left stopped (with a *Port in use* mark in the list) so you can free the port and start it later.
- **Recording** — record what a hub group streams into a folder under `C:\Volumes\Recordings\<name>`, and replay a recording into a hub group later. Each recording shows its duration, size and whether a recorder or player container is running. The recorder and player images are set in the settings.
- **DWIS modules** — scheduler, microstates, composer and the ADCS bridges, each with an inline editor for its `config.json`.
- **Clients** and **Advisors** — the web applications and advisor services. Each can run several times, one container per blackboard: when you create one you pick the blackboard it talks to (identified by its container name, with its OPC UA port shown next to it), and the container gets `BLACKBOARD_IP` and `BLACKBOARD_PORT` accordingly. The table shows one row per container with its blackboard, the blackboard port and a link to the app's own host port (the next free port from the module's default). The **new instance** button on a row creates another container of the same application against another blackboard.

**Hubs with a certificate from a company CA** need two extra things: one more line in the `docker run` command of the app, and the **Extra CA certificate file** setting (gear icon) for the containers the app launches. The steps are in [section 1](#hubs-with-a-certificate-from-a-company-ca).

Every container row has a **copy docker run command** button (terminal icon). It copies the `docker run` line equivalent to what the app creates for that container (image, name, ports, environment variables, bind mounts, labels and arguments), so the same container can be started by hand or in a script.

The bottom bar shows the Docker endpoint in use and the replication target. The gear opens the settings (Docker API endpoint, host volumes root, blackboard host IP, extra CA certificate file, polling interval). **Update application** pulls the latest `dwis-docker` image and recreates this container.

## 3. First test

This test launches a **Blackboard** that replicates to the **Blackboard Hub** hosted by NORCE. Real-time data streams in that hub, which makes it a good environment for early testing of any D-WIS installation.

1. Open the **Replication** tab. The NORCE hub is preconfigured and connects automatically. Make sure **Replicate** is on, the hub is **NORCE** and the group is `default`, then **Save** if you changed anything.
2. Open the **Standard setup** tab. In the **Blackboards** group, click the **create** button of the *Default Blackboard* row. The image is pulled if needed and the container is created and started. Give it a minute to warm up. The row now shows *Running* and `NORCE / default` on port 48030.
3. Back on the **Replication** tab, the `default` group now lists your blackboard among its connections.
4. Open the **Clients** tab and create **Log view** and **Blackboard explorer**. A dialog asks which blackboard each should use; keep the default blackboard. Docker images will be downloaded, which can take a few minutes.
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
