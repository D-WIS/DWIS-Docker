# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

DWIS-Docker is a container management tool for D-WIS (Drilling and Wells Interoperability Standards) services. It is a .NET 9 Blazor app that talks to the Docker engine on the host and lets the user create, start, stop, restart, update and inspect the D-WIS module containers (blackboards, scheduler, microstates, advice composer, 14 ADCS bridges, client web apps, advisor services), and manage the SignalR hubs the blackboards replicate to. It ships as the Docker image `digiwells/dwis-docker:stable` and is normally run as a container with the host's Docker socket mounted (see README.md). A WinForms desktop host renders the same UI.

## Commands

All paths are relative to `src/DWIS.Docker/`. The solution only contains this repo's projects, so it builds as a whole.

```bash
dotnet build DWIS.Docker.sln                                   # everything (Desktop project needs Windows)
dotnet test DWIS.Docker.Core.Tests                             # unit tests (container spec builder, image status)
dotnet test DWIS.Docker.Core.Tests --filter "FullyQualifiedName~ContainerSpecBuilderTests.Blackboard_with_replication"   # one test
dotnet run --project DWIS.Docker.Web --urls http://localhost:5272   # web host (needs Docker Desktop running)
dotnet run --project DWIS.Docker.Desktop                       # desktop host (Windows)

# Docker images (context must be src/DWIS.Docker: the Dockerfiles copy sibling projects and the .cer files)
docker build -f DWIS.Docker.Web/Dockerfile -t digiwells/dwis-docker:stable .
docker build -f DWIS.Docker.AppUpdater/Dockerfile -t digiwells/dwis-app-update:stable .
```

`.claude/launch.json` defines the `dwis-docker-web` preview server (same `dotnet run` as above). In development the web host stores its settings in `DWIS.Docker.Web/data/settings.json` (git-ignored); in the container they go to `/home/dwis-docker` (that is `C:\Volumes\dwis-docker` on the host).

## CI / release

`.github/workflows/publish-artifacts-and-docker-image.yml` runs only on manual `workflow_dispatch`: tests, a `win-x64` publish of the web app, and both Docker images pushed to Docker Hub. Nothing builds automatically on push or PR, so build and test locally before committing.

## Architecture

```
DWIS.Docker.Core  <-  DWIS.Docker.UI  <-  DWIS.Docker.Web      (ASP.NET Core, interactive server render mode)
                                      <-  DWIS.Docker.Desktop  (WinForms + BlazorWebView, net9.0-windows)
DWIS.Docker.Core  <-  DWIS.Docker.AppUpdater (console; the self-update helper container)
DWIS.Docker.Core  <-  DWIS.Docker.Core.Tests (xUnit)
```

### DWIS.Docker.Core (no UI dependencies)

- `Catalog/ModuleCatalog.cs` is the single source of truth for every predefined container: image repo and tag, container name, host config folder, config POCO type, ports. Ids are slugs (`blackboard`, `scheduler`, `bridge-autodriller`, `client-logview`, ...). `Catalog/ModuleConfigs.cs` holds the config POCOs; serialising `new T()` gives the default `config.json`.
- `Services/ContainerSpecBuilder.cs` is the **only** place that turns a `ModuleDefinition` + `RunContext` into Docker create parameters (name, command line, env, binds, ports, labels), and the only place that recognises containers. Containers we create carry `dwis.managed`, `dwis.module`, `dwis.kind` and, for blackboards, `dwis.hub`, `dwis.group`, `dwis.port` labels. Containers created by the previous version have no labels; they are matched by image and, for blackboards, by parsing `Config.Cmd` (`IsLegacy = true`).
- **Client / advisor instances.** A client or advisor module can run several times, each bound to one blackboard. `RunContext.Blackboard` (`BlackboardTarget`: blackboard container name + OPC UA port) makes `Build` add `BLACKBOARD_PORT=<port>` next to `BLACKBOARD_IP` and the labels `dwis.blackboard` / `dwis.blackboard.port`; web apps also get `dwis.port` (host port, empty when not published) because Docker lists no ports for stopped containers. The instance's own container name and host port come from `ModuleCatalog.Instance(module, name, hostPort)` (same module id, so `dwis.module` still resolves the catalog entry); the default name is `<module container name>-<blackboard name>`. `ContainerSpecBuilder.ToModuleInstance` reads this back into `ModuleInstance`; `ModuleStatus.Instances` lists every container of a module (oldest first, the one with the plain catalog name first) and `ModuleStatus.Container` is the first of them. `IModuleManager.CreateInstanceAsync` / `SuggestInstance` / `DefaultBlackboardTarget` are the instance API; plain `CreateAsync` of a web app binds it to the default blackboard.
- `Services/DockerRunCommandFormatter.cs` renders `CreateContainerParameters` as one `docker run` line (`-d`, `--name`, `--hostname`, `-it`, `-p`, `-e`, `-v`, `--add-host`, `-l`, image, args). The managers expose `RunCommand(...)` overloads that rebuild the parameters through the spec builder for an existing container (module instance, blackboard, local hub, replicator, recorder, player), so the copied command is exactly what the app would create, not what `docker inspect` shows.
- `Services/DockerService.cs` wraps `Docker.DotNet`. One `DockerClient`, rebuilt when the endpoint setting changes. Docker errors are translated into the `DwisDockerException` family (`Models/Exceptions.cs`) whose message is shown to the user as-is.
- `Services/ContainerMonitor.cs` (`IStatusService`) polls Docker on a timer and publishes an immutable `StatusSnapshot` (every module's status, every blackboard, in-flight operations). All views render from this snapshot; nothing in the UI talks to Docker directly. Registry lookups (`DockerHubRegistryService`, cached in `RegistryCache`) run in the background and decide `ImageStatus` by comparing local `RepoDigests` with the Docker Hub tag digest.
- `Services/ModuleManager.cs` (`IModuleManager`) is the façade the UI calls for every action (create, start, stop, restart, delete, update image, logs, create blackboard, self-update). It registers an `ActiveOperation` per call and refreshes the snapshot afterwards.
- `Services/HubManager.cs` (`IHubService`) keeps one SignalR `HubClient` per hub in the settings (MessagePack protocol, auto-reconnect) and polls `GetGroupData`. The DTOs `GroupDataDTO`/`GroupData` must stay **public** with the exact server property names.
- `Services/HubContainerService.cs` (`IHubContainerManager`): launches a hub server locally (`ContainerSpecBuilder.BuildLocalHub`: image `digiwells/dwissignalr`, port 8080 published on a host port, `--path`, `--source-hubs` / `--destination-hubs` as `url,remoteGroup[,localGroup];...`) and adds it to the settings as a hub with base URL `http://<BlackboardHostIp>:<port><path>` so both the app and other containers reach it; and starts standalone hub-to-hub replicators (`BuildHubReplication`: `--routes sourceUrl,sourceGroup,destinationUrl,destinationGroup;...`). Both are recognised purely from labels (`dwis.kind=hub` with `dwis.hub.id`, `dwis.kind=hubreplication` with `dwis.routes`); the monitor scans them into `StatusSnapshot.LocalHubs` / `HubReplications`. Sources: `DWIS-Blackboard-Server/src/DWIS.Server.SignalRHub` and `DWIS.Hub.HubReplication`; the replicator image does not exist yet, so image names are settings.
- `Services/RecordingService.cs`: `RecordingScanner` lists the folders under `<volumes root>/Recordings` (reading the recorder's `rtData` index for duration and chunk count) and matches them with recorder/player containers by the `dwis.recording` label; the monitor calls it every poll. `RecordingService` (`IRecordingManager`) starts/stops recorder and player containers (`ContainerSpecBuilder.BuildRecorder/BuildPlayer`: image from settings, `--hubURL/--hubGroup/--output-folder|--input-folder /recording`, folder bind-mounted at `/recording`, TTY on because both tools block on `Console.ReadLine`). The tools' source lives in the sibling repo `DWIS-Blackboard-Server/src/DWIS.Server.Recording.*`; their images do not exist yet, so the image names are settings.
- `Services/SettingsStore.cs`: `AppSettings` persisted as JSON in `HostEnvironment.DataDirectory`. `HostEnvironment` also distinguishes `LocalVolumesRoot` (where this process writes config files, `/home` in the container) from the host-side volumes root used as bind-mount source, auto-detected by inspecting the app's own container.
- `ServiceCollectionExtensions.AddDwisDockerCore()` registers everything; `DwisCoreOptions` turns off persistence, hubs or the monitor for the updater.

### DWIS.Docker.UI (Razor class library, MudBlazor 8)

- `DwisIcons.cs` and `DwisStates.cs` are the one icon/colour vocabulary. Never inline a Material icon in a component.
- `Components/DwisComponentBase.cs`: every live component inherits it. It re-renders on snapshot, settings, hub and action-runner changes and unsubscribes on dispose.
- `Services/UiActionRunner.cs` (scoped per circuit) runs user actions: busy state, pull progress, error snackbars, refresh afterwards. Components contain no try/catch.
- `Shared/ModuleTable.razor` (+ `BulkToolbar`, `ContainerActions`, `ConfigEditor`) is reused by Standard setup, DWIS modules, Clients and Advisors. Its rows are `ModuleRow`s: one per container of a module (`ModuleStatus.Instances`) plus a placeholder row for a module without any, so a client with two instances is two rows in every tab. Selection is tracked by row key (container id, or module id for a placeholder) and mutated in place: MudBlazor fires change callbacks whenever it sees a new collection reference, so replacing a bound collection in a callback creates an infinite render loop. Creating a client or advisor opens `CreateInstanceDialog` (pick a blackboard from the snapshot, name, host port); other modules are created directly.
- `ContainerActions` takes a `RunCommand` delegate; when set it shows the "copy docker run command" button (icon `DwisIcons.RunCommand`) and `DwisComponentBase.CopyRunCommandAsync` copies through `IPlatformServices` with a "Command copied" snackbar. Every container row (modules, blackboards, local hubs, replicators, recordings) passes one.
- `Shell/DwisShell.razor` is the root both hosts render; `Services/IPlatformServices.cs` is the only host-specific seam (clipboard, whether self-update is offered).

### Things that are easy to get wrong

- Bind-mount sources are resolved by the Docker daemon **on the host**. Inside the container, module folders must be given as the host path (detected from the app's own `/home` mount), never as `/home/...`.
- Blackboard labels are immutable: a blackboard created for group A shows "Differs from settings" after the target changes to group B. Updating its image recreates it with the new labels.
- The same holds for client/advisor instances: `dwis.blackboard` records the blackboard chosen at create time. Deleting or renaming that blackboard does not touch the instance; the chip in the Clients/Advisors tab turns orange when no blackboard of that name exists. "Update image" recreates an instance with its own name, blackboard and host port, never with the catalog defaults.
- The blackboard image (`digiwells/ddhubserver`) uses `latest`; every other module uses `stable`. When the `latest` tag moves, Docker lists old containers by image id; `ContainerMonitor.ResolveLegacyAsync` resolves the original name from `Config.Image`.
- `DOCKER_HOST=npipe://...` (from the README's `docker run`) is ignored on Linux with a warning.
- `MaximumReceiveMessageSize` is raised in the web host because the config editor sends whole JSON files over the circuit.
- Keep `NORCE_CA.cer` and `hub.docker.cer` in `src/DWIS.Docker/`; the web Dockerfile installs them so Docker Hub and the hub are reachable behind the NORCE firewall.
