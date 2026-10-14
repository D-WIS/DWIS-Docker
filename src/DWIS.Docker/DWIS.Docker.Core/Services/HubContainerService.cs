using Docker.DotNet.Models;
using DWIS.Docker.Core.Models;
using Microsoft.Extensions.Logging;

namespace DWIS.Docker.Core.Services;

/// <summary>Hub servers launched locally and standalone hub-to-hub replicators, both as containers.</summary>
public interface IHubContainerManager
{
    /// <summary>Base URL this app uses to reach a local hub on the given port (localhost on a desktop, the host alias inside the container).</summary>
    string LocalHubBaseUrl(int hostPort, string pathBase);

    /// <summary>Base URL other containers use to reach a local hub on the given port.</summary>
    string LocalHubContainerUrl(int hostPort, string pathBase);

    string SuggestLocalHubName();
    int SuggestLocalHubPort();

    /// <summary>Creates and starts the hub server container, then adds the hub to the settings so the app connects to it.</summary>
    Task CreateLocalHubAsync(LocalHubSpec spec, IProgress<PullProgress>? progress = null, CancellationToken ct = default);

    /// <summary>Removes the hub from the settings and, when it was launched locally, stops and removes its container.</summary>
    Task RemoveHubAsync(string hubId, CancellationToken ct = default);

    Task CreateReplicationAsync(HubReplicationSpec spec, IProgress<PullProgress>? progress = null, CancellationToken ct = default);
    Task DeleteReplicationAsync(string name, CancellationToken ct = default);
}

public sealed class HubContainerService : IHubContainerManager
{
    private readonly IDockerService _docker;
    private readonly ISettingsStore _settings;
    private readonly IStatusService _status;
    private readonly OperationTracker _operations;
    private readonly HostEnvironment _env;
    private readonly ILogger<HubContainerService> _logger;

    public HubContainerService(IDockerService docker, ISettingsStore settings, IStatusService status, OperationTracker operations, HostEnvironment env, ILogger<HubContainerService> logger)
    {
        _docker = docker;
        _settings = settings;
        _status = status;
        _operations = operations;
        _env = env;
        _logger = logger;
    }

    // From this app's point of view a published port is on localhost on a desktop; inside the
    // dwis-docker container it is on the host alias, like for every other container.
    public string LocalHubBaseUrl(int hostPort, string pathBase) =>
        _env.IsInContainer ? LocalHubContainerUrl(hostPort, pathBase) : $"http://localhost:{hostPort}{ContainerSpecBuilder.NormalizePath(pathBase)}";

    public string LocalHubContainerUrl(int hostPort, string pathBase) =>
        $"http://{_settings.Current.BlackboardHostIp}:{hostPort}{ContainerSpecBuilder.NormalizePath(pathBase)}";

    public string SuggestLocalHubName()
    {
        var names = _settings.Current.Hubs.Select(h => h.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = "Local hub";
        for (var i = 2; names.Contains(name); i++) name = $"Local hub {i}";
        return name;
    }

    public int SuggestLocalHubPort()
    {
        var used = _status.Current.LocalHubs.Select(h => h.HostPort).ToHashSet();
        var port = 5290;
        while (used.Contains(port)) port++;
        return port;
    }

    public Task CreateLocalHubAsync(LocalHubSpec spec, IProgress<PullProgress>? progress = null, CancellationToken ct = default)
    {
        var name = spec.Name.Trim();
        if (name.Length == 0) throw new DwisDockerException("Hub name must not be empty.");
        if (spec.HostPort is < 1024 or > 65535) throw new DwisDockerException("Port must be between 1024 and 65535.");
        var hubId = ContainerSpecBuilder.LocalHubId(name);
        var settings = _settings.Current;
        if (settings.Hubs.Any(h => h.Id == hubId || h.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ContainerConflictException($"A hub named '{name}' already exists.");
        if (_status.Current.LocalHubs.Any(h => h.HostPort == spec.HostPort))
            throw new PortInUseException($"Port {spec.HostPort} is already used by another local hub.");

        return Track("hub:" + hubId, $"Launching hub {name}", async token =>
        {
            var image = settings.LocalHubImage;
            var p = ContainerSpecBuilder.BuildLocalHub(spec, image, hubId, LinuxEngine);
            var id = await CreateAsync(p, image, progress, token).ConfigureAwait(false);
            await _docker.StartAsync(id, token).ConfigureAwait(false);

            var appUrl = LocalHubBaseUrl(spec.HostPort, spec.PathBase);
            var containerUrl = LocalHubContainerUrl(spec.HostPort, spec.PathBase);
            var updated = _settings.Current.Clone();
            updated.Hubs.Add(new HubDefinition(hubId, name, appUrl, AutoConnect: true, ContainerBaseUrl: appUrl == containerUrl ? null : containerUrl));
            await _settings.SaveAsync(updated, token).ConfigureAwait(false);
        }, ct);
    }

    public Task RemoveHubAsync(string hubId, CancellationToken ct = default) =>
        Track("hub:" + hubId, "Removing hub", async token =>
        {
            var local = _status.Current.LocalHub(hubId);
            if (local is not null)
                await RemoveContainerAsync(local.Container, token).ConfigureAwait(false);

            var updated = _settings.Current.Clone();
            if (updated.Hubs.RemoveAll(h => h.Id == hubId) > 0)
            {
                if (updated.ReplicationHubId == hubId)
                {
                    updated.ReplicationHubId = updated.Hubs.FirstOrDefault()?.Id;
                    if (updated.ReplicationHubId is null) updated.ReplicationEnabled = false;
                }
                await _settings.SaveAsync(updated, token).ConfigureAwait(false);
            }
        }, ct);

    public Task CreateReplicationAsync(HubReplicationSpec spec, IProgress<PullProgress>? progress = null, CancellationToken ct = default)
    {
        var name = spec.Name.Trim();
        if (name.Length == 0) throw new DwisDockerException("Replication name must not be empty.");
        if (_status.Current.HubReplications.Any(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ContainerConflictException($"A replication named '{name}' already exists.");
        foreach (var r in spec.Routes)
        {
            if (new[] { r.SourceUrl, r.SourceGroup, r.DestinationUrl, r.DestinationGroup }.Any(string.IsNullOrWhiteSpace))
                throw new DwisDockerException("Every route needs a source hub, source group, destination hub and destination group.");
            if (string.Equals(r.SourceUrl.TrimEnd('/'), r.DestinationUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) && r.SourceGroup == r.DestinationGroup)
                throw new DwisDockerException($"Route {r.Describe()} has the same source and destination.");
        }

        return Track("replication:" + name, $"Starting replication {name}", async token =>
        {
            var image = _settings.Current.HubReplicationImage;
            var p = ContainerSpecBuilder.BuildHubReplication(spec with { Name = name }, image, LinuxEngine);
            var id = await CreateAsync(p, image, progress, token).ConfigureAwait(false);
            await _docker.StartAsync(id, token).ConfigureAwait(false);
        }, ct);
    }

    public Task DeleteReplicationAsync(string name, CancellationToken ct = default) =>
        Track("replication:" + name, $"Deleting replication {name}", async token =>
        {
            var rep = _status.Current.HubReplications.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                      ?? throw new NotFoundException($"Unknown replication '{name}'.");
            await RemoveContainerAsync(rep.Container, token).ConfigureAwait(false);
        }, ct);

    // ---------------------------------------------------------------- helpers

    private bool LinuxEngine => string.Equals(_status.Current.Engine.OsType, "linux", StringComparison.OrdinalIgnoreCase);

    private async Task<string> CreateAsync(CreateContainerParameters p, string imageRef, IProgress<PullProgress>? progress, CancellationToken ct)
    {
        var repo = ContainerSpecBuilder.ImageRepo(imageRef);
        var tag = imageRef.Length > repo.Length + 1 ? imageRef[(repo.Length + 1)..] : "latest";
        var images = await _docker.ListImagesAsync(ct).ConfigureAwait(false);
        if (!images.Any(i => i.RepoTags.Contains(imageRef, StringComparer.OrdinalIgnoreCase)))
            await _docker.PullImageAsync(repo, tag, progress, ct).ConfigureAwait(false);
        return await _docker.CreateRawAsync(p, ct).ConfigureAwait(false);
    }

    private async Task RemoveContainerAsync(ContainerInfo c, CancellationToken ct)
    {
        if (c.IsRunning) await _docker.StopAsync(c.Id, ct).ConfigureAwait(false);
        await _docker.RemoveAsync(c.Id, force: true, ct).ConfigureAwait(false);
    }

    private async Task Track(string key, string description, Func<CancellationToken, Task> work, CancellationToken ct)
    {
        using var _ = _operations.Begin(key, description);
        try
        {
            await work(ct).ConfigureAwait(false);
        }
        catch (DwisDockerException ex)
        {
            _logger.LogWarning("{Description} failed: {Message}", description, ex.UserMessage);
            throw;
        }
        finally
        {
            try { await _status.RefreshAsync(CancellationToken.None).ConfigureAwait(false); }
            catch (Exception ex) { _logger.LogDebug(ex, "Refresh after '{Description}' failed", description); }
        }
    }
}
