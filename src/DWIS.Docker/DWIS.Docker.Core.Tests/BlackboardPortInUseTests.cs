using Docker.DotNet.Models;
using DWIS.Docker.Core.Catalog;
using DWIS.Docker.Core.Models;
using DWIS.Docker.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ContainerState = DWIS.Docker.Core.Models.ContainerState;

namespace DWIS.Docker.Core.Tests;

/// <summary>A port that is already in use must not prevent creating a blackboard.</summary>
public class BlackboardPortInUseTests
{
    private const int Port = 48030;

    private static readonly BlackboardSpec NewBoard = new("blackboard-rig1", Port, HubId: null, Group: null, WritePassword: null, StartAfterCreate: true);

    // ---------------------------------------------------------------- creating

    [Fact]
    public async Task Port_of_another_blackboard_does_not_refuse_the_creation()
    {
        var docker = new FakeDocker { StartError = new PortInUseException("Bind for 0.0.0.0:48030 failed: port is already allocated") };
        var manager = Manager(docker, Snapshot(blackboards: [Blackboard("blackboard", Port, running: true)]));

        var result = await manager.CreateBlackboardAsync(NewBoard);

        Assert.Equal(["blackboard-rig1"], docker.Created);          // the container exists
        Assert.Equal(["id-blackboard-rig1"], docker.StartAttempts); // and a start was tried
        Assert.False(result.Started);
        Assert.NotNull(result.Warning);
        Assert.Contains("port 48030", result.Warning);
        Assert.Contains("'blackboard'", result.Warning);
        Assert.Contains("created", result.Warning);
    }

    [Fact]
    public async Task Port_taken_by_another_program_also_keeps_the_container()
    {
        var docker = new FakeDocker { StartError = new PortInUseException("ports are not available: exposing port TCP 0.0.0.0:48030") };
        var manager = Manager(docker, Snapshot());

        var result = await manager.CreateBlackboardAsync(NewBoard);

        Assert.Equal(["blackboard-rig1"], docker.Created);
        Assert.False(result.Started);
        Assert.Contains("another program", result.Warning);
    }

    [Fact]
    public async Task Without_start_a_taken_port_gives_a_plain_created_container()
    {
        var docker = new FakeDocker { StartError = new PortInUseException("port is already allocated") };
        var manager = Manager(docker, Snapshot(blackboards: [Blackboard("blackboard", Port, running: true)]));

        var result = await manager.CreateBlackboardAsync(NewBoard with { StartAfterCreate = false });

        Assert.Equal(["blackboard-rig1"], docker.Created);
        Assert.Empty(docker.StartAttempts);
        Assert.False(result.Started);
        Assert.Null(result.Warning);
    }

    [Fact]
    public async Task Free_port_creates_and_starts_without_warning()
    {
        var docker = new FakeDocker();
        var manager = Manager(docker, Snapshot(blackboards: [Blackboard("blackboard", Port, running: true)]));

        var result = await manager.CreateBlackboardAsync(NewBoard with { Port = 48031 });

        Assert.Equal(["blackboard-rig1"], docker.Created);
        Assert.True(result.Started);
        Assert.Null(result.Warning);
    }

    [Fact]
    public async Task Created_container_publishes_the_chosen_port_even_when_it_is_taken()
    {
        var docker = new FakeDocker { StartError = new PortInUseException("port is already allocated") };
        var manager = Manager(docker, Snapshot(blackboards: [Blackboard("blackboard", Port, running: true)]));

        await manager.CreateBlackboardAsync(NewBoard);

        var spec = Assert.Single(docker.Specs);
        Assert.Equal("48030", spec.HostConfig.PortBindings["48030/tcp"][0].HostPort);
        Assert.Equal("48030", spec.Labels[ContainerSpecBuilder.LabelPort]);
    }

    [Fact]
    public async Task Other_start_failures_are_still_errors()
    {
        var docker = new FakeDocker { StartError = new DwisDockerException("Could not start container: no such image") };
        var manager = Manager(docker, Snapshot());

        var ex = await Assert.ThrowsAsync<DwisDockerException>(() => manager.CreateBlackboardAsync(NewBoard));

        Assert.Contains("no such image", ex.UserMessage);
    }

    [Fact]
    public async Task Status_is_refreshed_after_a_start_that_failed_on_the_port()
    {
        var docker = new FakeDocker { StartError = new PortInUseException("port is already allocated") };
        var status = new FakeStatus(Snapshot());
        var manager = Manager(docker, status);

        await manager.CreateBlackboardAsync(NewBoard);

        Assert.Equal(1, status.Refreshes); // so the new, stopped blackboard shows in the list at once
    }

    // ---------------------------------------------------------------- who holds a port

    [Fact]
    public void Port_holders_cover_blackboards_module_instances_and_local_hubs()
    {
        var logView = ModuleCatalog.Clients.Single(c => c.Id == "client-logview");
        var web = Container("logview", running: true);
        var hub = Container("hub-rig", running: false);
        var snapshot = Snapshot(
            blackboards: [Blackboard("blackboard", 5290, running: true)],
            modules: [new ModuleStatus(logView, web, true, ImageStatus.Unknown, null, null, false, [new ModuleInstance(web, null, 5290, false)])],
            hubs: [new LocalHubInfo("local-rig", hub, 5290, "", [], [])]);

        Assert.Equal(["blackboard", "hub-rig", "logview"], snapshot.ContainersOnHostPort(5290));
        Assert.Equal(["blackboard", "logview"], snapshot.ContainersOnHostPort(5290, runningOnly: true));
        Assert.Equal(["hub-rig", "logview"], snapshot.ContainersOnHostPort(5290, exceptContainerId: "id-blackboard"));
        Assert.Empty(snapshot.ContainersOnHostPort(5291));
    }

    [Fact]
    public void Port_holders_see_a_port_docker_reports_but_no_label_records()
    {
        var logView = ModuleCatalog.Clients.Single(c => c.Id == "client-logview");
        var web = Container("logview", running: true, ports: [new PublishedPort(8080, 6000)]);
        var snapshot = Snapshot(modules: [new ModuleStatus(logView, web, true, ImageStatus.Unknown, null, null, false, [new ModuleInstance(web, null, 5276, false)])]);

        Assert.Equal(["logview"], snapshot.ContainersOnHostPort(6000));
    }

    [Fact]
    public void A_blackboard_listed_twice_is_named_once()
    {
        // The catalog blackboard is in the blackboard list and is also an instance of its module.
        var board = Blackboard("blackboard", Port, running: true);
        var snapshot = Snapshot(
            blackboards: [board],
            modules: [new ModuleStatus(ModuleCatalog.Blackboard, board.Container, true, ImageStatus.Unknown, null, board, false, [new ModuleInstance(board.Container, null, Port, false)])]);

        Assert.Equal(["blackboard"], snapshot.ContainersOnHostPort(Port));
    }

    // ---------------------------------------------------------------- recognising Docker's message

    [Theory]
    [InlineData("driver failed programming external connectivity on endpoint bb: Bind for 0.0.0.0:48030 failed: port is already allocated")]
    [InlineData("failed to bind host port for 0.0.0.0:48030:172.17.0.2:48030/tcp: address already in use")]
    [InlineData("ports are not available: exposing port TCP 0.0.0.0:48030 -> 127.0.0.1:0: listen tcp 0.0.0.0:48030: bind: An attempt was made to access a socket in a way forbidden by its access permissions.")]
    [InlineData("listen tcp 0.0.0.0:48030: bind: Only one usage of each socket address (protocol/network address/port) is normally permitted.")]
    public void Docker_messages_about_a_taken_port_are_recognised(string message) =>
        Assert.True(DockerService.IsPortInUse(message));

    [Theory]
    [InlineData("No such image: digiwells/ddhubserver:latest")]
    [InlineData("Conflict. The container name \"/blackboard\" is already in use by container abc")]
    public void Other_docker_messages_are_not_taken_for_a_port_problem(string message) =>
        Assert.False(DockerService.IsPortInUse(message));

    // ---------------------------------------------------------------- helpers

    private static ModuleManager Manager(FakeDocker docker, StatusSnapshot snapshot) => Manager(docker, new FakeStatus(snapshot));

    private static ModuleManager Manager(FakeDocker docker, FakeStatus status)
    {
        var settings = new InMemorySettingsStore();
        var env = new HostEnvironment(
            new DwisCoreOptions { DataDirectory = Path.Combine(Path.GetTempPath(), "dwis-docker-tests") },
            NullLogger<HostEnvironment>.Instance);
        return new ModuleManager(docker, status, settings, new ConfigFileService(env, settings), new OperationTracker(), env, NullLogger<ModuleManager>.Instance);
    }

    private static StatusSnapshot Snapshot(
        IReadOnlyList<BlackboardInfo>? blackboards = null,
        IReadOnlyList<ModuleStatus>? modules = null,
        IReadOnlyList<LocalHubInfo>? hubs = null) =>
        StatusSnapshot.Empty with { Blackboards = blackboards ?? [], Modules = modules ?? [], LocalHubs = hubs ?? [] };

    private static BlackboardInfo Blackboard(string name, int port, bool running) =>
        new(Container(name, running), port, HubUrl: null, Group: null, IsLegacy: false);

    private static ContainerInfo Container(string name, bool running, IReadOnlyList<PublishedPort>? ports = null) =>
        new($"id-{name}", name, running ? ContainerState.Running : ContainerState.Exited, running ? "Up" : "Exited",
            "img", "sha256:img", new Dictionary<string, string>(), DateTimeOffset.UtcNow, ports);

    private sealed class FakeStatus(StatusSnapshot snapshot) : IStatusService
    {
        public StatusSnapshot Current { get; } = snapshot;
        public int Refreshes { get; private set; }
#pragma warning disable CS0067 // never raised in these tests
        public event EventHandler<StatusSnapshot>? Changed;
#pragma warning restore CS0067

        public Task RefreshAsync(CancellationToken ct = default)
        {
            Refreshes++;
            return Task.CompletedTask;
        }

        public Task CheckForUpdatesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    /// <summary>Records what the manager asks of Docker. Only the calls the blackboard create path makes are implemented.</summary>
    private sealed class FakeDocker : IDockerService
    {
        public List<string> Created { get; } = [];
        public List<CreateContainerParameters> Specs { get; } = [];
        public List<string> StartAttempts { get; } = [];
        public Exception? StartError { get; init; }

        public string EndpointDescription => "fake";

        public Task<ContainerInfo> CreateContainerAsync(ModuleDefinition module, RunContext ctx, IProgress<PullProgress>? progress, CancellationToken ct = default)
        {
            var spec = ContainerSpecBuilder.Build(module, ctx);
            Specs.Add(spec);
            Created.Add(spec.Name);
            return Task.FromResult(new ContainerInfo($"id-{spec.Name}", spec.Name, ContainerState.Created, "Created", spec.Image, "sha256:img",
                new Dictionary<string, string>(spec.Labels), DateTimeOffset.UtcNow));
        }

        public Task StartAsync(string containerId, CancellationToken ct = default)
        {
            StartAttempts.Add(containerId);
            return StartError is null ? Task.CompletedTask : Task.FromException(StartError);
        }

        public Task<EngineStatus> PingAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ContainerInspectResponse> InspectAsync(string containerId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LocalImageInfo>> ListImagesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> CreateRawAsync(CreateContainerParameters parameters, CancellationToken ct = default) => throw new NotSupportedException();
        public Task StopAsync(string containerId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RestartAsync(string containerId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RemoveAsync(string containerId, bool force, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> GetLogsAsync(string containerId, int tailLines, CancellationToken ct = default) => throw new NotSupportedException();
        public Task PullImageAsync(string repo, string tag, IProgress<PullProgress>? progress, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> RemoveImageAsync(string imageRef, bool force, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RecreateContainersOfImageAsync(string imageRef, IProgress<PullProgress>? progress, CancellationToken ct = default) => throw new NotSupportedException();
        public Task LaunchSelfUpdaterAsync(string appImageRef, string updaterImageRef, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> DetectHostVolumesRootAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }
}
