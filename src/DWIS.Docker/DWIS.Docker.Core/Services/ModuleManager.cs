using System.Collections.Concurrent;
using DWIS.Docker.Core.Catalog;
using DWIS.Docker.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DWIS.Docker.Core.Services;

/// <summary>Everything the UI does to containers goes through here.</summary>
public interface IModuleManager
{
    RunContext BuildRunContext();

    /// <summary>
    /// Writes the default config if missing, pulls the image if needed and creates the container (not started).
    /// A client or advisor is bound to the default blackboard (see <see cref="DefaultBlackboardTarget"/>).
    /// </summary>
    Task CreateAsync(ModuleDefinition module, IProgress<PullProgress>? progress = null, CancellationToken ct = default);
    Task CreateAndStartAsync(ModuleDefinition module, IProgress<PullProgress>? progress = null, CancellationToken ct = default);

    /// <summary>Stops, removes, re-pulls and recreates the module's primary container, keeping its name, blackboard and port. A running container is started again.</summary>
    Task UpdateImageAsync(ModuleDefinition module, IProgress<PullProgress>? progress = null, CancellationToken ct = default);
    /// <summary>Same as <see cref="UpdateImageAsync"/> for one instance of a module.</summary>
    Task UpdateInstanceImageAsync(ModuleInstance instance, IProgress<PullProgress>? progress = null, CancellationToken ct = default);
    Task UpdateBlackboardImageAsync(BlackboardInfo blackboard, IProgress<PullProgress>? progress = null, CancellationToken ct = default);

    // ---------------------------------------------------------------- client / advisor instances

    /// <summary>The blackboard clients and advisors are bound to when no other is chosen: the catalog blackboard (existing or as it would be created).</summary>
    BlackboardTarget DefaultBlackboardTarget();

    /// <summary>A unique container name and a free host port for a new instance of the module against the blackboard.</summary>
    InstanceSpec SuggestInstance(ModuleDefinition module, BlackboardTarget blackboard);

    /// <summary>Creates one instance of a client or advisor against the chosen blackboard (BLACKBOARD_IP / BLACKBOARD_PORT).</summary>
    Task CreateInstanceAsync(InstanceSpec spec, IProgress<PullProgress>? progress = null, CancellationToken ct = default);

    // ---------------------------------------------------------------- docker run commands

    /// <summary>The <c>docker run</c> command equivalent to what the app creates for the module (its primary container, or a new one).</summary>
    string RunCommand(ModuleDefinition module, ModuleInstance? instance = null);
    string RunCommand(InstanceSpec spec);
    string RunCommand(BlackboardInfo blackboard);

    Task<IReadOnlyList<OperationFailure>> CreateMissingAsync(IEnumerable<ModuleDefinition> modules, IProgress<PullProgress>? progress = null, CancellationToken ct = default);
    Task<IReadOnlyList<OperationFailure>> BulkAsync(IEnumerable<ContainerInfo> containers, BulkAction action, CancellationToken ct = default);

    Task StartAsync(string containerId, CancellationToken ct = default);
    Task StopAsync(string containerId, CancellationToken ct = default);
    Task RestartAsync(string containerId, CancellationToken ct = default);
    Task DeleteAsync(string containerId, CancellationToken ct = default);
    Task<string> GetLogsAsync(string containerId, int? tailLines = null, CancellationToken ct = default);

    Task CreateBlackboardAsync(BlackboardSpec spec, IProgress<PullProgress>? progress = null, CancellationToken ct = default);
    BlackboardSpec SuggestBlackboard(string? hubId, string? group);

    Task UpdateApplicationAsync(CancellationToken ct = default);
}

public sealed class ModuleManager : IModuleManager
{
    private readonly IDockerService _docker;
    private readonly IStatusService _status;
    private readonly ISettingsStore _settings;
    private readonly IConfigFileService _config;
    private readonly OperationTracker _operations;
    private readonly HostEnvironment _env;
    private readonly ILogger<ModuleManager> _logger;
    private readonly CancellationToken _shutdown;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public ModuleManager(
        IDockerService docker,
        IStatusService status,
        ISettingsStore settings,
        IConfigFileService config,
        OperationTracker operations,
        HostEnvironment env,
        ILogger<ModuleManager> logger,
        IHostApplicationLifetime? lifetime = null)
    {
        _docker = docker;
        _status = status;
        _settings = settings;
        _config = config;
        _operations = operations;
        _env = env;
        _logger = logger;
        _shutdown = lifetime?.ApplicationStopping ?? CancellationToken.None;
    }

    // ---------------------------------------------------------------- context

    public RunContext BuildRunContext()
    {
        var s = _settings.Current;
        ReplicationTarget? replication = null;
        if (s.ReplicationEnabled && s.ReplicationHub is { } hub)
            replication = new ReplicationTarget(hub.UrlForContainers, s.ReplicationGroup, s.ReplicationGroupPassword);

        var root = string.IsNullOrWhiteSpace(s.HostVolumesRoot) ? _env.DefaultHostVolumesRoot : s.HostVolumesRoot!;
        var engine = _status.Current.Engine;
        var linuxEngine = string.Equals(engine.OsType, "linux", StringComparison.OrdinalIgnoreCase);
        return new RunContext(s.BlackboardHostIp, root, replication, linuxEngine);
    }

    // ---------------------------------------------------------------- create / update

    public Task CreateAsync(ModuleDefinition module, IProgress<PullProgress>? progress = null, CancellationToken ct = default) =>
        CreateCoreAsync(module, start: false, progress, ct);

    public Task CreateAndStartAsync(ModuleDefinition module, IProgress<PullProgress>? progress = null, CancellationToken ct = default) =>
        CreateCoreAsync(module, start: true, progress, ct);

    private Task CreateCoreAsync(ModuleDefinition module, bool start, IProgress<PullProgress>? progress, CancellationToken ct)
    {
        var (definition, ctx) = module.IsWebApp ? Resolve(SuggestInstance(module, DefaultBlackboardTarget())) : (module, BuildRunContext());
        return WithModuleLock(module.Id, $"Creating {module.DisplayName}", async token =>
        {
            await _config.EnsureDefaultAsync(definition, token).ConfigureAwait(false);
            var c = await _docker.CreateContainerAsync(definition, ctx, progress, token).ConfigureAwait(false);
            if (start) await _docker.StartAsync(c.Id, token).ConfigureAwait(false);
        }, ct);
    }

    public Task UpdateImageAsync(ModuleDefinition module, IProgress<PullProgress>? progress = null, CancellationToken ct = default) =>
        WithModuleLock(module.Id, $"Updating {module.DisplayName}", async token =>
        {
            var current = _status.Current.Find(module.Id);
            var instance = current?.Instances.FirstOrDefault();
            var (definition, ctx) = ForExisting(module, instance);
            await RecreateAsync(definition, ctx, instance?.Container, progress, token).ConfigureAwait(false);
        }, ct);

    public Task UpdateInstanceImageAsync(ModuleInstance instance, IProgress<PullProgress>? progress = null, CancellationToken ct = default)
    {
        var c = instance.Container;
        var module = ModuleOf(c);
        var (definition, ctx) = ForExisting(module, instance);
        return WithModuleLock(c.Id, $"Updating {c.Name}", token => RecreateAsync(definition, ctx, c, progress, token), ct);
    }

    /// <summary>Definition and context that recreate an existing container as it is: same name, blackboard and host port.</summary>
    private (ModuleDefinition definition, RunContext ctx) ForExisting(ModuleDefinition module, ModuleInstance? instance)
    {
        var ctx = BuildRunContext();
        if (instance is null || module.IsBlackboard) return (module, ctx);
        if (!module.IsWebApp && instance.Blackboard is null) return (module, ctx);
        var definition = ModuleCatalog.Instance(module, instance.Container.Name, instance.HostPort ?? module.HostPort);
        return (definition, ctx with { Blackboard = instance.Blackboard });
    }

    private static ModuleDefinition ModuleOf(ContainerInfo c) =>
        ContainerSpecBuilder.ResolveModuleId(c) is { } id && ModuleCatalog.Find(id) is { } module
            ? module
            : throw new DwisDockerException($"'{c.Name}' is not a container of a known module.");

    public Task UpdateBlackboardImageAsync(BlackboardInfo blackboard, IProgress<PullProgress>? progress = null, CancellationToken ct = default)
    {
        var c = blackboard.Container;
        var (module, ctx) = ForBlackboard(blackboard);
        if (module.Id == ModuleCatalog.Blackboard.Id)
            return UpdateImageAsync(ModuleCatalog.Blackboard, progress, ct);
        return WithModuleLock(c.Id, $"Updating {c.Name}", token => RecreateAsync(module, ctx, c, progress, token), ct);
    }

    /// <summary>Definition and context that recreate a blackboard as it is: the catalog one from the settings, any other with its own name, port, hub and group.</summary>
    private (ModuleDefinition module, RunContext ctx) ForBlackboard(BlackboardInfo blackboard)
    {
        var c = blackboard.Container;
        var isCatalog = c.Labels.TryGetValue(ContainerSpecBuilder.LabelModule, out var id) && id == ModuleCatalog.Blackboard.Id;
        var status = isCatalog ? _status.Current.Find(ModuleCatalog.Blackboard.Id) : null;
        var baseCtx = BuildRunContext();
        if (status?.Container?.Id == c.Id)
            return (ModuleCatalog.Blackboard, baseCtx);

        // Ad hoc or legacy: keep its own name, port, hub and group.
        var module = ModuleCatalog.AdHocBlackboard(c.Name, blackboard.Port);
        var s = _settings.Current;
        ReplicationTarget? replication = null;
        if (blackboard.IsReplicating)
        {
            var samePlace = s.ReplicationHub?.Matches(blackboard.HubUrl) == true
                            && blackboard.Group == s.ReplicationGroup;
            replication = new ReplicationTarget(blackboard.HubUrl!, blackboard.Group ?? "default", samePlace ? s.ReplicationGroupPassword : null);
        }
        return (module, baseCtx with { Replication = replication });
    }

    private async Task RecreateAsync(ModuleDefinition module, RunContext ctx, ContainerInfo? existing, IProgress<PullProgress>? progress, CancellationToken ct)
    {
        var wasRunning = existing?.IsRunning == true;
        if (existing is not null)
        {
            if (wasRunning) await _docker.StopAsync(existing.Id, ct).ConfigureAwait(false);
            await _docker.RemoveAsync(existing.Id, force: true, ct).ConfigureAwait(false);
        }
        await _docker.RemoveImageAsync(module.ImageRef, force: false, ct).ConfigureAwait(false);
        await _docker.PullImageAsync(module.ImageRepo, module.ImageTag, progress, ct).ConfigureAwait(false);
        await _config.EnsureDefaultAsync(module, ct).ConfigureAwait(false);
        var created = await _docker.CreateContainerAsync(module, ctx, progress, ct).ConfigureAwait(false);
        if (wasRunning || existing is null)
            await _docker.StartAsync(created.Id, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OperationFailure>> CreateMissingAsync(IEnumerable<ModuleDefinition> modules, IProgress<PullProgress>? progress = null, CancellationToken ct = default)
    {
        var failures = new List<OperationFailure>();
        var ordered = modules.OrderBy(m => m.IsBlackboard ? 0 : 1).ToList();
        foreach (var module in ordered)
        {
            if (_status.Current.Find(module.Id)?.Exists == true) continue;
            try
            {
                await CreateAsync(module, progress, ct).ConfigureAwait(false);
            }
            catch (DwisDockerException ex)
            {
                failures.Add(new OperationFailure(module.Id, $"{module.DisplayName}: {ex.UserMessage}"));
            }
        }
        return failures;
    }

    public async Task<IReadOnlyList<OperationFailure>> BulkAsync(IEnumerable<ContainerInfo> containers, BulkAction action, CancellationToken ct = default)
    {
        var failures = new List<OperationFailure>();
        var list = containers.ToList();
        if (action == BulkAction.Remove)
            list.Reverse();
        foreach (var c in list)
        {
            try
            {
                var task = action switch
                {
                    BulkAction.Start => StartAsync(c.Id, ct),
                    BulkAction.Stop => StopAsync(c.Id, ct),
                    BulkAction.Restart => RestartAsync(c.Id, ct),
                    BulkAction.Remove => DeleteAsync(c.Id, ct),
                    _ => throw new ArgumentOutOfRangeException(nameof(action))
                };
                await task.ConfigureAwait(false);
            }
            catch (DwisDockerException ex)
            {
                failures.Add(new OperationFailure(c.Id, $"{c.Name}: {ex.UserMessage}"));
            }
        }
        return failures;
    }

    // ---------------------------------------------------------------- lifecycle

    public Task StartAsync(string containerId, CancellationToken ct = default) =>
        Track(containerId, "Starting", token => _docker.StartAsync(containerId, token), ct);

    public Task StopAsync(string containerId, CancellationToken ct = default) =>
        Track(containerId, "Stopping", token => _docker.StopAsync(containerId, token), ct);

    public Task RestartAsync(string containerId, CancellationToken ct = default) =>
        Track(containerId, "Restarting", token => _docker.RestartAsync(containerId, token), ct);

    public Task DeleteAsync(string containerId, CancellationToken ct = default) =>
        Track(containerId, "Deleting", token => _docker.RemoveAsync(containerId, force: true, token), ct);

    public Task<string> GetLogsAsync(string containerId, int? tailLines = null, CancellationToken ct = default) =>
        _docker.GetLogsAsync(containerId, tailLines ?? _settings.Current.LogTailLines, ct);

    // ---------------------------------------------------------------- blackboards

    public Task CreateBlackboardAsync(BlackboardSpec spec, IProgress<PullProgress>? progress = null, CancellationToken ct = default)
    {
        var name = spec.ContainerName.Trim();
        if (name.Length == 0) throw new DwisDockerException("Container name must not be empty.");
        if (_status.Current.Blackboards.Any(b => b.Port == spec.Port))
            throw new PortInUseException($"Port {spec.Port} is already used by another blackboard.");

        var s = _settings.Current;
        ReplicationTarget? replication = null;
        if (spec.HubId is not null)
        {
            var hub = s.Hubs.FirstOrDefault(h => h.Id == spec.HubId)
                      ?? throw new DwisDockerException("Unknown hub.");
            replication = new ReplicationTarget(hub.UrlForContainers, string.IsNullOrWhiteSpace(spec.Group) ? "default" : spec.Group.Trim(), spec.WritePassword);
        }

        var module = ModuleCatalog.AdHocBlackboard(name, spec.Port);
        var ctx = BuildRunContext() with { Replication = replication };
        return WithModuleLock(name, $"Creating blackboard {name}", async token =>
        {
            var c = await _docker.CreateContainerAsync(module, ctx, progress, token).ConfigureAwait(false);
            if (spec.StartAfterCreate)
                await _docker.StartAsync(c.Id, token).ConfigureAwait(false);
        }, ct);
    }

    public BlackboardSpec SuggestBlackboard(string? hubId, string? group)
    {
        var snapshot = _status.Current;
        var used = snapshot.Blackboards.Select(b => b.Port).ToHashSet();
        var port = ModuleCatalog.DefaultBlackboardPort;
        while (used.Contains(port)) port++;

        var s = _settings.Current;
        var effectiveHub = hubId ?? (s.ReplicationEnabled ? s.ReplicationHubId : null);
        var effectiveGroup = group ?? s.ReplicationGroup;
        var baseName = effectiveHub is null ? "blackboard-local" : $"blackboard-{Slug(effectiveGroup)}";
        var names = snapshot.Blackboards.Select(b => b.Container.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = baseName;
        for (var i = 2; names.Contains(name); i++) name = $"{baseName}-{i}";

        var samePlace = effectiveHub == s.ReplicationHubId && effectiveGroup == s.ReplicationGroup;
        return new BlackboardSpec(name, port, effectiveHub, effectiveHub is null ? null : effectiveGroup,
            samePlace ? s.ReplicationGroupPassword : null, StartAfterCreate: true);
    }

    // ---------------------------------------------------------------- client / advisor instances

    public BlackboardTarget DefaultBlackboardTarget()
    {
        var status = _status.Current.Find(ModuleCatalog.Blackboard.Id);
        if (status?.Blackboard is { } existing)
            return new BlackboardTarget(existing.Container.Name, existing.Port);
        var ctx = BuildRunContext();
        return new BlackboardTarget(ContainerSpecBuilder.ContainerNameFor(ModuleCatalog.Blackboard, ctx), ModuleCatalog.Blackboard.HostPort ?? ModuleCatalog.DefaultBlackboardPort);
    }

    public InstanceSpec SuggestInstance(ModuleDefinition module, BlackboardTarget blackboard)
    {
        var snapshot = _status.Current;
        var names = snapshot.Modules.SelectMany(m => m.Instances).Select(i => i.Container.Name)
            .Concat(snapshot.Blackboards.Select(b => b.Container.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var baseName = ContainerSpecBuilder.InstanceContainerName(module, blackboard.ContainerName);
        var name = baseName;
        for (var i = 2; names.Contains(name); i++) name = $"{baseName}-{i}";

        int? port = null;
        if (module.HostPort is { } start)
        {
            var used = UsedHostPorts(snapshot);
            port = start;
            while (used.Contains(port.Value)) port++;
        }
        return new InstanceSpec(module.Id, name, blackboard, port, StartAfterCreate: true);
    }

    private static HashSet<int> UsedHostPorts(StatusSnapshot snapshot)
    {
        var used = new HashSet<int>();
        foreach (var m in snapshot.Modules)
            foreach (var i in m.Instances)
            {
                if (i.HostPort is { } hp) used.Add(hp);
                foreach (var p in i.Container.PublishedPorts) used.Add(p.HostPort);
            }
        foreach (var b in snapshot.Blackboards) used.Add(b.Port);
        foreach (var h in snapshot.LocalHubs) used.Add(h.HostPort);
        return used;
    }

    public Task CreateInstanceAsync(InstanceSpec spec, IProgress<PullProgress>? progress = null, CancellationToken ct = default)
    {
        var (definition, ctx) = Resolve(spec);
        var snapshot = _status.Current;
        if (snapshot.Modules.SelectMany(m => m.Instances).Any(i => i.Container.Name.Equals(definition.ContainerName, StringComparison.OrdinalIgnoreCase)))
            throw new ContainerConflictException($"A container named '{definition.ContainerName}' already exists.");
        if (spec.HostPort is { } port && UsedHostPorts(snapshot).Contains(port))
            throw new PortInUseException($"Port {port} is already used by another container.");

        return WithModuleLock(definition.ContainerName, $"Creating {definition.DisplayName} for {spec.Blackboard.ContainerName}", async token =>
        {
            await _config.EnsureDefaultAsync(definition, token).ConfigureAwait(false);
            var c = await _docker.CreateContainerAsync(definition, ctx, progress, token).ConfigureAwait(false);
            if (spec.StartAfterCreate)
                await _docker.StartAsync(c.Id, token).ConfigureAwait(false);
        }, ct);
    }

    /// <summary>Validates an instance request and turns it into the definition and context the create path needs.</summary>
    private (ModuleDefinition definition, RunContext ctx) Resolve(InstanceSpec spec)
    {
        var module = ModuleCatalog.Find(spec.ModuleId) ?? throw new DwisDockerException($"Unknown module '{spec.ModuleId}'.");
        var name = spec.ContainerName.Trim();
        if (name.Length == 0) throw new DwisDockerException("Container name must not be empty.");
        if (string.IsNullOrWhiteSpace(spec.Blackboard.ContainerName)) throw new DwisDockerException("Choose a blackboard.");
        if (spec.Blackboard.Port is < 1 or > 65535) throw new DwisDockerException("The blackboard port is not valid.");
        if (spec.HostPort is { } port && port is < 1 or > 65535) throw new DwisDockerException("Port must be between 1 and 65535.");
        var definition = ModuleCatalog.Instance(module, name, spec.HostPort);
        return (definition, BuildRunContext() with { Blackboard = spec.Blackboard });
    }

    // ---------------------------------------------------------------- docker run commands

    public string RunCommand(ModuleDefinition module, ModuleInstance? instance = null)
    {
        instance ??= _status.Current.Find(module.Id)?.Instances.FirstOrDefault();
        if (instance is null && module.IsWebApp)
            return RunCommand(SuggestInstance(module, DefaultBlackboardTarget()));
        var (definition, ctx) = ForExisting(module, instance);
        return DockerRunCommandFormatter.Format(ContainerSpecBuilder.Build(definition, ctx));
    }

    public string RunCommand(InstanceSpec spec)
    {
        var (definition, ctx) = Resolve(spec);
        return DockerRunCommandFormatter.Format(ContainerSpecBuilder.Build(definition, ctx));
    }

    public string RunCommand(BlackboardInfo blackboard)
    {
        var (module, ctx) = ForBlackboard(blackboard);
        return DockerRunCommandFormatter.Format(ContainerSpecBuilder.Build(module, ctx));
    }

    // ---------------------------------------------------------------- self update

    public Task UpdateApplicationAsync(CancellationToken ct = default) =>
        Track("app", "Launching updater", token => _docker.LaunchSelfUpdaterAsync(ModuleCatalog.AppImageRef, ModuleCatalog.UpdaterImageRef, token), ct, refresh: false);

    // ---------------------------------------------------------------- helpers

    private Task WithModuleLock(string key, string description, Func<CancellationToken, Task> work, CancellationToken ct) =>
        Track(key, description, async token =>
        {
            var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            if (!await gate.WaitAsync(0, token).ConfigureAwait(false))
                throw new DwisDockerException($"{description} is already in progress.");
            try { await work(token).ConfigureAwait(false); }
            finally { gate.Release(); }
        }, ct);

    private async Task Track(string key, string description, Func<CancellationToken, Task> work, CancellationToken ct, bool refresh = true)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _shutdown);
        using var _ = _operations.Begin(key, description);
        try
        {
            await work(linked.Token).ConfigureAwait(false);
        }
        catch (DwisDockerException ex)
        {
            _logger.LogWarning("{Description} failed: {Message}", description, ex.UserMessage);
            throw;
        }
        finally
        {
            if (refresh && !_shutdown.IsCancellationRequested)
            {
                try { await _status.RefreshAsync(CancellationToken.None).ConfigureAwait(false); }
                catch (Exception ex) { _logger.LogDebug(ex, "Refresh after '{Description}' failed", description); }
            }
        }
    }

    private static string Slug(string s) =>
        new(s.Trim().Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_').ToArray());
}
