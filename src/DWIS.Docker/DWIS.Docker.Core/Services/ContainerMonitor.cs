using DWIS.Docker.Core.Catalog;
using DWIS.Docker.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DWIS.Docker.Core.Services;

/// <summary>Produces the shared <see cref="StatusSnapshot"/> that every view renders from.</summary>
public interface IStatusService
{
    StatusSnapshot Current { get; }

    /// <summary>Raised on a background thread after each refresh.</summary>
    event EventHandler<StatusSnapshot>? Changed;

    /// <summary>Requests a refresh and waits for a full one that started after this call.</summary>
    Task RefreshAsync(CancellationToken ct = default);

    /// <summary>Re-queries the registry for every catalog image, then refreshes.</summary>
    Task CheckForUpdatesAsync(CancellationToken ct = default);
}

public sealed class ContainerMonitor : IStatusService, IHostedService, IDisposable
{
    private readonly IDockerService _docker;
    private readonly ISettingsStore _settings;
    private readonly IConfigFileService _config;
    private readonly RegistryCache _registry;
    private readonly OperationTracker _operations;
    private readonly HostEnvironment _env;
    private readonly RecordingScanner _recordings;
    private readonly ILogger<ContainerMonitor> _logger;

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly SemaphoreSlim _wake = new(0, 1);
    private TaskCompletionSource _nextRefresh = NewTcs();
    private CancellationTokenSource? _loopCts;
    private Task? _loop;
    private int _registryFetchRunning;
    private bool _lastEngineReachable = true;
    private StatusSnapshot _current = StatusSnapshot.Empty;
    private readonly Dictionary<string, (string imageRef, IList<string>? cmd)> _legacyDetails = new();

    public ContainerMonitor(
        IDockerService docker,
        ISettingsStore settings,
        IConfigFileService config,
        RegistryCache registry,
        OperationTracker operations,
        HostEnvironment env,
        RecordingScanner recordings,
        ILogger<ContainerMonitor> logger)
    {
        _docker = docker;
        _settings = settings;
        _config = config;
        _registry = registry;
        _operations = operations;
        _env = env;
        _recordings = recordings;
        _logger = logger;
        _operations.Changed += OnOperationsChanged;
        _settings.Changed += (_, _) => Wake();
    }

    public StatusSnapshot Current => Volatile.Read(ref _current);

    public event EventHandler<StatusSnapshot>? Changed;

    // ---------------------------------------------------------------- hosted service

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_loop is not null) return Task.CompletedTask;
        _loopCts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_loopCts.Token), CancellationToken.None);
        _ = FetchRegistryAsync(force: false, _loopCts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_loopCts is null || _loop is null) return;
        _loopCts.Cancel();
        try { await _loop.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        _loop = null;
    }

    public void Dispose()
    {
        _loopCts?.Cancel();
        _operations.Changed -= OnOperationsChanged;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var tcs = Interlocked.Exchange(ref _nextRefresh, NewTcs());
            try
            {
                await RefreshCoreAsync(ct).ConfigureAwait(false);
                tcs.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled();
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while refreshing status");
                tcs.TrySetException(ex);
            }

            try
            {
                await _wake.WaitAsync(_settings.Current.PollingInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static TaskCompletionSource NewTcs() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void Wake()
    {
        if (_wake.CurrentCount == 0)
        {
            try { _wake.Release(); } catch (SemaphoreFullException) { }
        }
    }

    // ---------------------------------------------------------------- public API

    public Task RefreshAsync(CancellationToken ct = default)
    {
        if (_loop is null)
            return RefreshCoreAsync(ct);
        var tcs = Volatile.Read(ref _nextRefresh);
        Wake();
        return tcs.Task.WaitAsync(ct);
    }

    public async Task CheckForUpdatesAsync(CancellationToken ct = default)
    {
        await FetchRegistryAsync(force: true, ct).ConfigureAwait(false);
        await RefreshAsync(ct).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------- refresh

    private async Task RefreshCoreAsync(CancellationToken ct)
    {
        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var snapshot = await BuildSnapshotAsync(ct).ConfigureAwait(false);
            Publish(snapshot);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private void Publish(StatusSnapshot snapshot)
    {
        Volatile.Write(ref _current, snapshot);
        try
        {
            Changed?.Invoke(this, snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "A status subscriber threw");
        }
    }

    private void OnOperationsChanged()
    {
        var cur = Current;
        Publish(cur with { Operations = _operations.Current });
    }

    private async Task<StatusSnapshot> BuildSnapshotAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var previous = Current;
        var engine = await _docker.PingAsync(ct).ConfigureAwait(false);

        if (!engine.Reachable)
        {
            if (_lastEngineReachable)
                _logger.LogWarning("Docker engine unreachable: {Error}", engine.Error);
            _lastEngineReachable = false;
            return previous with { TakenAt = now, Engine = engine, Operations = _operations.Current };
        }
        if (!_lastEngineReachable)
            _logger.LogInformation("Docker engine reachable again ({Version})", engine.Version);
        _lastEngineReachable = true;

        if (_env.IsInContainer && !_env.HostVolumesRootIsKnown)
            await _docker.DetectHostVolumesRootAsync(ct).ConfigureAwait(false);

        var listed = await _docker.ListContainersAsync(ct).ConfigureAwait(false);
        var images = await _docker.ListImagesAsync(ct).ConfigureAwait(false);
        var imagesByTag = new Dictionary<string, LocalImageInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var img in images)
            foreach (var tag in img.RepoTags)
                imagesByTag[tag] = img;

        // Containers created by the old app have no labels. Docker lists them by image id once the
        // tag has moved on, so resolve the original image name (and command line) by inspecting once.
        var containers = new List<ContainerInfo>(listed.Count);
        foreach (var c in listed)
            containers.Add(await ResolveLegacyAsync(c, ct).ConfigureAwait(false));
        lock (_legacyDetails)
        {
            var ids = listed.Select(c => c.Id).ToHashSet();
            foreach (var gone in _legacyDetails.Keys.Where(id => !ids.Contains(id)).ToList())
                _legacyDetails.Remove(gone);
        }

        // Blackboards: ours by label, the old app's by image plus command line (hub/group/port).
        var blackboards = new List<BlackboardInfo>();
        foreach (var c in containers.Where(ContainerSpecBuilder.LooksLikeBlackboard).OrderBy(c => c.CreatedAt))
        {
            IList<string>? cmd = null;
            if (!ContainerSpecBuilder.IsManaged(c))
            {
                lock (_legacyDetails)
                {
                    if (_legacyDetails.TryGetValue(c.Id, out var details)) cmd = details.cmd;
                }
            }
            blackboards.Add(ContainerSpecBuilder.ToBlackboardInfo(c, cmd));
        }

        var settings = _settings.Current;
        var target = settings.ReplicationEnabled ? settings.ReplicationHub : null;
        var expectedHub = target?.UrlForContainers;
        var expectedGroup = target is null ? null : settings.ReplicationGroup;
        var catalogBlackboardNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleCatalog.Blackboard.ContainerName };
        if (expectedGroup is not null)
            catalogBlackboardNames.Add(ContainerSpecBuilder.ContainerNameFor(ModuleCatalog.Blackboard,
                new RunContext("", "", new ReplicationTarget(expectedHub!, expectedGroup, null), false)));

        var byModule = containers
            .Where(c => !ContainerSpecBuilder.LooksLikeBlackboard(c))
            .Select(c => (id: ContainerSpecBuilder.ResolveModuleId(c), c))
            .Where(x => x.id is not null)
            .GroupBy(x => x.id!)
            .ToDictionary(g => g.Key, g => g.Select(x => x.c).OrderBy(c => c.CreatedAt).ToList());

        var modules = new List<ModuleStatus>(ModuleCatalog.All.Count);
        foreach (var module in ModuleCatalog.All)
        {
            ContainerInfo? container = null;
            BlackboardInfo? blackboard = null;
            var duplicates = 0;
            var mismatch = false;

            if (module.IsBlackboard)
            {
                var expectedName = ContainerSpecBuilder.ContainerNameFor(module,
                    new RunContext("", "", expectedGroup is null ? null : new ReplicationTarget(expectedHub!, expectedGroup, null), false));
                var mine = blackboards
                    .Where(b => b.Container.Labels.TryGetValue(ContainerSpecBuilder.LabelModule, out var id)
                        ? id == module.Id
                        : b.IsLegacy && catalogBlackboardNames.Contains(b.Container.Name))
                    .OrderBy(b => b.Container.Name.Equals(expectedName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(b => b.Container.CreatedAt)
                    .ToList();
                blackboard = mine.FirstOrDefault();
                container = blackboard?.Container;
                duplicates = Math.Max(0, mine.Count - 1);
                if (blackboard is not null)
                    mismatch = !string.Equals(blackboard.HubUrl?.TrimEnd('/'), expectedHub?.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                               || !string.Equals(blackboard.Group, expectedGroup, StringComparison.Ordinal);
            }
            else if (byModule.TryGetValue(module.Id, out var list))
            {
                container = list[0];
                duplicates = list.Count - 1;
            }

            imagesByTag.TryGetValue(module.ImageRef, out var local);
            var imageStatus = RegistryCache.Compare(local, _registry.Peek(module.ImageRef));

            modules.Add(new ModuleStatus(
                module,
                container,
                local is not null,
                imageStatus,
                module.HasConfig ? _config.Exists(module) : null,
                blackboard,
                mismatch,
                duplicates));
        }

        if (ModuleCatalog.All.Any(m => !_registry.IsFresh(m.ImageRef)))
            _ = FetchRegistryAsync(force: false, _loopCts?.Token ?? ct);

        IReadOnlyList<RecordingInfo> recordings;
        try
        {
            recordings = _recordings.Scan(containers);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not scan the recordings folder");
            recordings = previous.Recordings;
        }

        var localHubs = ContainerSpecBuilder.ScanLocalHubs(containers);
        var replications = ContainerSpecBuilder.ScanHubReplications(containers);

        return new StatusSnapshot(now, engine, modules, blackboards, recordings, localHubs, replications, _operations.Current);
    }

    /// <summary>
    /// For containers without our labels: resolves the image name Docker no longer shows (it lists the
    /// image id once the tag has moved) and remembers the command line, with one inspect per container.
    /// </summary>
    private async Task<ContainerInfo> ResolveLegacyAsync(ContainerInfo c, CancellationToken ct)
    {
        if (ContainerSpecBuilder.IsManaged(c)) return c;
        var needsImage = LooksLikeImageId(c.ImageRef);
        if (!needsImage && !ContainerSpecBuilder.LooksLikeBlackboard(c)) return c;

        (string imageRef, IList<string>? cmd) details;
        bool known;
        lock (_legacyDetails) known = _legacyDetails.TryGetValue(c.Id, out details);
        if (!known)
        {
            try
            {
                var ins = await _docker.InspectAsync(c.Id, ct).ConfigureAwait(false);
                details = (ins.Config?.Image is { Length: > 0 } img ? img : c.ImageRef, ins.Config?.Cmd);
            }
            catch (DwisDockerException)
            {
                return c; // vanished between list and inspect
            }
            lock (_legacyDetails) _legacyDetails[c.Id] = details;
        }
        return needsImage ? c with { ImageRef = details.imageRef } : c;
    }

    private static bool LooksLikeImageId(string imageRef) =>
        imageRef.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
        || (imageRef.Length >= 12 && !imageRef.Contains('/') && !imageRef.Contains(':') && imageRef.All(Uri.IsHexDigit));

    private async Task FetchRegistryAsync(bool force, CancellationToken ct)
    {
        if (Interlocked.CompareExchange(ref _registryFetchRunning, 1, 0) != 0) return;
        try
        {
            var images = ModuleCatalog.All.Select(m => (m.ImageRepo, m.ImageTag)).Distinct().ToList();
            using var throttle = new SemaphoreSlim(4);
            var tasks = images.Select(async img =>
            {
                await throttle.WaitAsync(ct).ConfigureAwait(false);
                try { await _registry.GetAsync(img.ImageRepo, img.ImageTag, force, ct).ConfigureAwait(false); }
                finally { throttle.Release(); }
            });
            await Task.WhenAll(tasks).ConfigureAwait(false);
            Wake();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Registry refresh failed");
        }
        finally
        {
            Interlocked.Exchange(ref _registryFetchRunning, 0);
        }
    }
}
