using System.Net;
using System.Net.Sockets;
using Docker.DotNet;
using Docker.DotNet.Models;
using DWIS.Docker.Core.Catalog;
using DWIS.Docker.Core.Models;
using ContainerState = DWIS.Docker.Core.Models.ContainerState;
using Microsoft.Extensions.Logging;

namespace DWIS.Docker.Core.Services;

/// <summary>Thin, typed wrapper around the Docker engine API.</summary>
public interface IDockerService
{
    /// <summary>The endpoint currently in use (for display).</summary>
    string EndpointDescription { get; }

    Task<EngineStatus> PingAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken ct = default);
    Task<ContainerInspectResponse> InspectAsync(string containerId, CancellationToken ct = default);
    Task<IReadOnlyList<LocalImageInfo>> ListImagesAsync(CancellationToken ct = default);

    /// <summary>The single create path for catalog modules. Pulls the image when absent. The container is not started.</summary>
    Task<ContainerInfo> CreateContainerAsync(ModuleDefinition module, RunContext ctx, IProgress<PullProgress>? progress, CancellationToken ct = default);

    /// <summary>Creates a container from ready-made parameters (recorder, player). Returns its id. Does not pull.</summary>
    Task<string> CreateRawAsync(CreateContainerParameters parameters, CancellationToken ct = default);

    Task StartAsync(string containerId, CancellationToken ct = default);
    Task StopAsync(string containerId, CancellationToken ct = default);
    Task RestartAsync(string containerId, CancellationToken ct = default);
    Task RemoveAsync(string containerId, bool force, CancellationToken ct = default);
    Task<string> GetLogsAsync(string containerId, int tailLines, CancellationToken ct = default);

    Task PullImageAsync(string repo, string tag, IProgress<PullProgress>? progress, CancellationToken ct = default);
    Task<bool> RemoveImageAsync(string imageRef, bool force, CancellationToken ct = default);

    /// <summary>Stops, removes, re-pulls and recreates every container of the image, keeping name and config. Used by the updater.</summary>
    Task RecreateContainersOfImageAsync(string imageRef, IProgress<PullProgress>? progress, CancellationToken ct = default);

    /// <summary>Starts a throwaway updater container that will recreate this app's container.</summary>
    Task LaunchSelfUpdaterAsync(string appImageRef, string updaterImageRef, CancellationToken ct = default);

    /// <summary>Inside the container: inspects the app's own container and returns the host path mounted on /home.</summary>
    Task<string?> DetectHostVolumesRootAsync(CancellationToken ct = default);
}

public sealed class DockerService : IDockerService, IDisposable
{
    private readonly ISettingsStore _settings;
    private readonly HostEnvironment _env;
    private readonly ILogger<DockerService> _logger;
    private readonly object _gate = new();
    private DockerClient? _client;
    private string? _clientEndpoint;

    public DockerService(ISettingsStore settings, HostEnvironment env, ILogger<DockerService> logger)
    {
        _settings = settings;
        _env = env;
        _logger = logger;
        _settings.Changed += (_, _) => InvalidateClient();
    }

    public string EndpointDescription => ResolveEndpoint() ?? "default engine socket";

    // ---------------------------------------------------------------- client

    private DockerClient Client
    {
        get
        {
            var endpoint = ResolveEndpoint();
            lock (_gate)
            {
                if (_client is not null && _clientEndpoint == endpoint) return _client;
                _client?.Dispose();
                var conf = endpoint is null
                    ? new DockerClientConfiguration()
                    : new DockerClientConfiguration(new Uri(endpoint));
                _client = conf.CreateClient();
                _clientEndpoint = endpoint;
                _logger.LogInformation("Docker client created for {Endpoint}", endpoint ?? "default socket");
                return _client;
            }
        }
    }

    private void InvalidateClient()
    {
        lock (_gate)
        {
            _clientEndpoint = "\0invalidated";
        }
    }

    private string? ResolveEndpoint()
    {
        var configured = _settings.Current.DockerEndpoint;
        if (!string.IsNullOrWhiteSpace(configured)) return configured.Trim();

        var env = _env.DockerHostEnv;
        if (string.IsNullOrWhiteSpace(env)) return null;
        if (!_env.IsWindows && env.StartsWith("npipe://", StringComparison.OrdinalIgnoreCase))
        {
            // The README passes the Windows named pipe into the Linux container; it is meaningless there.
            return null;
        }
        if (env.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
            env = "http://" + env[6..];
        return env;
    }

    public void Dispose()
    {
        lock (_gate) _client?.Dispose();
    }

    // ---------------------------------------------------------------- queries

    public async Task<EngineStatus> PingAsync(CancellationToken ct = default)
    {
        try
        {
            var v = await Client.System.GetSystemInfoAsync(ct).ConfigureAwait(false);
            return new EngineStatus(true, v.ServerVersion, v.OSType, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return EngineStatus.Unreachable(Translate(ex).UserMessage);
        }
    }

    public async Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(CancellationToken ct = default)
    {
        var list = await Run(() => Client.Containers.ListContainersAsync(new ContainersListParameters { All = true }, ct)).ConfigureAwait(false);
        return list.Select(ToInfo).ToList();
    }

    public Task<ContainerInspectResponse> InspectAsync(string containerId, CancellationToken ct = default) =>
        Run(() => Client.Containers.InspectContainerAsync(containerId, ct));

    public async Task<IReadOnlyList<LocalImageInfo>> ListImagesAsync(CancellationToken ct = default)
    {
        var list = await Run(() => Client.Images.ListImagesAsync(new ImagesListParameters { All = false }, ct)).ConfigureAwait(false);
        return list.Select(i => new LocalImageInfo(
            i.ID,
            (i.RepoTags ?? []).ToList(),
            (i.RepoDigests ?? []).ToList(),
            new DateTimeOffset(i.Created, TimeSpan.Zero))).ToList();
    }

    // ---------------------------------------------------------------- container lifecycle

    public async Task<ContainerInfo> CreateContainerAsync(ModuleDefinition module, RunContext ctx, IProgress<PullProgress>? progress, CancellationToken ct = default)
    {
        var spec = ContainerSpecBuilder.Build(module, ctx);
        await EnsureImageAsync(module.ImageRepo, module.ImageTag, progress, ct).ConfigureAwait(false);
        var response = await Run(() => Client.Containers.CreateContainerAsync(spec, ct), $"create container '{spec.Name}'").ConfigureAwait(false);
        foreach (var w in response.Warnings ?? [])
            _logger.LogWarning("Docker warning creating {Name}: {Warning}", spec.Name, w);
        var inspect = await InspectAsync(response.ID, ct).ConfigureAwait(false);
        return ToInfo(inspect);
    }

    public async Task<string> CreateRawAsync(CreateContainerParameters parameters, CancellationToken ct = default)
    {
        var response = await Run(() => Client.Containers.CreateContainerAsync(parameters, ct), $"create container '{parameters.Name}'").ConfigureAwait(false);
        foreach (var w in response.Warnings ?? [])
            _logger.LogWarning("Docker warning creating {Name}: {Warning}", parameters.Name, w);
        return response.ID;
    }

    public Task StartAsync(string containerId, CancellationToken ct = default) =>
        Run(() => Client.Containers.StartContainerAsync(containerId, new ContainerStartParameters(), ct), "start container");

    public Task StopAsync(string containerId, CancellationToken ct = default) =>
        Run(() => Client.Containers.StopContainerAsync(containerId, new ContainerStopParameters { WaitBeforeKillSeconds = 10 }, ct), "stop container");

    public Task RestartAsync(string containerId, CancellationToken ct = default) =>
        Run(() => Client.Containers.RestartContainerAsync(containerId, new ContainerRestartParameters { WaitBeforeKillSeconds = 10 }, ct), "restart container");

    public Task RemoveAsync(string containerId, bool force, CancellationToken ct = default) =>
        Run(() => Client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = force }, ct), "remove container");

    public async Task<string> GetLogsAsync(string containerId, int tailLines, CancellationToken ct = default)
    {
        var inspect = await InspectAsync(containerId, ct).ConfigureAwait(false);
        var tty = inspect.Config?.Tty ?? false;
        var parameters = new ContainerLogsParameters
        {
            ShowStdout = true,
            ShowStderr = true,
            Timestamps = true,
            Tail = tailLines.ToString()
        };
        using var stream = await Run(() => Client.Containers.GetContainerLogsAsync(containerId, tty, parameters, ct), "read logs").ConfigureAwait(false);
        var (stdout, stderr) = await stream.ReadOutputToEndAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(stderr)) return stdout;
        if (string.IsNullOrEmpty(stdout)) return stderr;
        return stdout + Environment.NewLine + stderr;
    }

    // ---------------------------------------------------------------- images

    public async Task PullImageAsync(string repo, string tag, IProgress<PullProgress>? progress, CancellationToken ct = default)
    {
        var imageRef = $"{repo}:{tag}";
        var aggregator = new PullProgressAggregator(imageRef, progress);
        try
        {
            await Client.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = repo, Tag = tag },
                null,
                aggregator,
                ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ImagePullException($"Could not pull {imageRef}: {Translate(ex).UserMessage}", ex);
        }
        if (aggregator.Error is { } error)
            throw new ImagePullException($"Could not pull {imageRef}: {error}");
        progress?.Report(new PullProgress(imageRef, "Pull complete", null, null, null, 1.0));
    }

    public async Task<bool> RemoveImageAsync(string imageRef, bool force, CancellationToken ct = default)
    {
        try
        {
            await Client.Images.DeleteImageAsync(imageRef, new ImageDeleteParameters { Force = force }, ct).ConfigureAwait(false);
            return true;
        }
        catch (DockerImageNotFoundException)
        {
            return false;
        }
        catch (DockerApiException ex)
        {
            _logger.LogWarning("Could not remove image {Image}: {Message}", imageRef, ex.Message);
            return false;
        }
    }

    private async Task EnsureImageAsync(string repo, string tag, IProgress<PullProgress>? progress, CancellationToken ct)
    {
        var imageRef = $"{repo}:{tag}";
        var images = await ListImagesAsync(ct).ConfigureAwait(false);
        if (images.Any(i => i.RepoTags.Contains(imageRef, StringComparer.OrdinalIgnoreCase))) return;
        await PullImageAsync(repo, tag, progress, ct).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------- updater

    public async Task RecreateContainersOfImageAsync(string imageRef, IProgress<PullProgress>? progress, CancellationToken ct = default)
    {
        var all = await Run(() => Client.Containers.ListContainersAsync(new ContainersListParameters { All = true }, ct)).ConfigureAwait(false);
        var targets = all.Where(c => string.Equals(c.Image, imageRef, StringComparison.OrdinalIgnoreCase)).ToList();
        _logger.LogInformation("Recreating {Count} container(s) of {Image}", targets.Count, imageRef);

        var kept = new List<(string name, bool wasRunning, ContainerInspectResponse inspect)>();
        foreach (var c in targets)
        {
            var ins = await InspectAsync(c.ID, ct).ConfigureAwait(false);
            kept.Add((ins.Name.TrimStart('/'), ins.State?.Running == true, ins));
        }

        foreach (var (name, wasRunning, ins) in kept)
        {
            if (wasRunning)
            {
                _logger.LogInformation("Stopping {Name}", name);
                await StopAsync(ins.ID, ct).ConfigureAwait(false);
            }
            _logger.LogInformation("Removing {Name}", name);
            await RemoveAsync(ins.ID, force: true, ct).ConfigureAwait(false);
        }

        await RemoveImageAsync(imageRef, force: true, ct).ConfigureAwait(false);
        var (repo, tag) = SplitRef(imageRef);
        await PullImageAsync(repo, tag, progress, ct).ConfigureAwait(false);

        foreach (var (name, wasRunning, ins) in kept)
        {
            var config = ins.Config;
            if (config.Hostname is { Length: 12 } h && ins.ID.StartsWith(h, StringComparison.OrdinalIgnoreCase))
                config.Hostname = null; // let Docker assign a new one
            config.Image = imageRef;

            var create = new CreateContainerParameters(config)
            {
                Name = name,
                HostConfig = ins.HostConfig,
                NetworkingConfig = ins.NetworkSettings?.Networks is { Count: > 0 } nets
                    ? new NetworkingConfig { EndpointsConfig = nets }
                    : null
            };
            _logger.LogInformation("Creating {Name} from {Image}", name, imageRef);
            var created = await Run(() => Client.Containers.CreateContainerAsync(create, ct), $"recreate '{name}'").ConfigureAwait(false);
            if (wasRunning)
            {
                _logger.LogInformation("Starting {Name}", name);
                await StartAsync(created.ID, ct).ConfigureAwait(false);
            }
        }
    }

    public async Task LaunchSelfUpdaterAsync(string appImageRef, string updaterImageRef, CancellationToken ct = default)
    {
        var (repo, tag) = SplitRef(updaterImageRef);
        await PullImageAsync(repo, tag, null, ct).ConfigureAwait(false);

        var p = new CreateContainerParameters
        {
            Name = $"dwis-app-update-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
            Image = updaterImageRef,
            Cmd = [appImageRef],
            User = "root",
            Tty = true,
            HostConfig = new HostConfig
            {
                AutoRemove = true,
                Binds = ["/var/run/docker.sock:/var/run/docker.sock"]
            }
        };
        var created = await Run(() => Client.Containers.CreateContainerAsync(p, ct), "create updater").ConfigureAwait(false);
        await StartAsync(created.ID, ct).ConfigureAwait(false);
        _logger.LogInformation("Updater container {Id} started for {Image}", created.ID[..12], appImageRef);
    }

    public async Task<string?> DetectHostVolumesRootAsync(CancellationToken ct = default)
    {
        if (!_env.IsInContainer) return null;
        try
        {
            ContainerInspectResponse? self = null;
            var hint = _env.OwnContainerIdHint();
            if (hint is not null)
            {
                try { self = await Client.Containers.InspectContainerAsync(hint, ct).ConfigureAwait(false); }
                catch (DockerContainerNotFoundException) { /* fall back to search */ }
            }
            if (self is null)
            {
                var all = await Client.Containers.ListContainersAsync(new ContainersListParameters { All = false }, ct).ConfigureAwait(false);
                foreach (var c in all.Where(c => c.Image.Contains("dwis-docker", StringComparison.OrdinalIgnoreCase)))
                {
                    var ins = await Client.Containers.InspectContainerAsync(c.ID, ct).ConfigureAwait(false);
                    if (string.Equals(ins.Config?.Hostname, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
                    {
                        self = ins;
                        break;
                    }
                }
            }
            var mount = self?.Mounts?.FirstOrDefault(m => m.Destination == HostEnvironment.ContainerVolumesMount);
            if (mount?.Source is { Length: > 0 } source)
            {
                _env.RecordDetectedHostVolumesRoot(source);
                return source;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not detect the host volumes root");
        }
        return null;
    }

    // ---------------------------------------------------------------- helpers

    private async Task<T> Run<T>(Func<Task<T>> call, string? what = null)
    {
        try
        {
            return await call().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ex is not DwisDockerException)
        {
            throw Translate(ex, what);
        }
    }

    private async Task Run(Func<Task> call, string? what = null)
    {
        try
        {
            await call().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ex is not DwisDockerException)
        {
            throw Translate(ex, what);
        }
    }

    private DwisDockerException Translate(Exception ex, string? what = null)
    {
        var prefix = what is null ? "" : $"Could not {what}: ";
        switch (ex)
        {
            case DockerContainerNotFoundException:
                return new NotFoundException(prefix + "container not found (it may have been removed).", ex);
            case DockerImageNotFoundException:
                return new NotFoundException(prefix + "image not found.", ex);
            case DockerApiException api:
                var body = Clean(api.ResponseBody) ?? api.Message;
                if (api.StatusCode == HttpStatusCode.Conflict)
                    return new ContainerConflictException(prefix + body, ex);
                if (body.Contains("port is already allocated", StringComparison.OrdinalIgnoreCase)
                    || body.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
                    return new PortInUseException(prefix + body, ex);
                return new DwisDockerException(prefix + body, ex);
            case HttpRequestException or SocketException or TimeoutException or IOException:
                _logger.LogDebug(ex, "Docker engine unreachable");
                return new DockerUnavailableException($"Docker engine unreachable at {EndpointDescription}: {Root(ex).Message}", ex);
            case AggregateException agg when agg.InnerException is not null:
                return Translate(agg.InnerException, what);
            default:
                return new DwisDockerException(prefix + Root(ex).Message, ex);
        }
    }

    private static Exception Root(Exception ex)
    {
        while (ex.InnerException is not null) ex = ex.InnerException;
        return ex;
    }

    private static string? Clean(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        body = body.Trim();
        // {"message":"..."}
        if (body.StartsWith('{'))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("message", out var m)) return m.GetString();
            }
            catch { /* not json */ }
        }
        return body;
    }

    private static (string repo, string tag) SplitRef(string imageRef)
    {
        var repo = ContainerSpecBuilder.ImageRepo(imageRef);
        var tag = imageRef.Length > repo.Length + 1 ? imageRef[(repo.Length + 1)..] : "latest";
        return (repo, tag);
    }

    private static ContainerInfo ToInfo(ContainerListResponse c) => new(
        c.ID,
        (c.Names?.FirstOrDefault() ?? c.ID).TrimStart('/'),
        ParseState(c.State),
        c.Status ?? "",
        c.Image,
        c.ImageID,
        new Dictionary<string, string>(c.Labels ?? new Dictionary<string, string>()),
        new DateTimeOffset(c.Created, TimeSpan.Zero),
        (c.Ports ?? [])
            .Where(p => p.PublicPort > 0 && string.Equals(p.Type, "tcp", StringComparison.OrdinalIgnoreCase))
            .Select(p => new PublishedPort(p.PrivatePort, p.PublicPort))
            .Distinct()
            .OrderBy(p => p.ContainerPort)
            .ToList());

    private static ContainerInfo ToInfo(ContainerInspectResponse c) => new(
        c.ID,
        c.Name.TrimStart('/'),
        ParseState(c.State?.Status),
        c.State?.Status ?? "",
        c.Config?.Image ?? c.Image,
        c.Image,
        new Dictionary<string, string>(c.Config?.Labels ?? new Dictionary<string, string>()),
        c.Created,
        PublishedPorts(c.NetworkSettings?.Ports ?? c.HostConfig?.PortBindings));

    private static List<PublishedPort> PublishedPorts(IDictionary<string, IList<PortBinding>>? bindings)
    {
        var result = new List<PublishedPort>();
        if (bindings is null) return result;
        foreach (var (key, list) in bindings)
        {
            if (list is null || !key.EndsWith("/tcp", StringComparison.OrdinalIgnoreCase)) continue;
            if (!int.TryParse(key[..^4], out var containerPort)) continue;
            foreach (var b in list)
                if (int.TryParse(b.HostPort, out var hostPort) && hostPort > 0)
                    result.Add(new PublishedPort(containerPort, hostPort));
        }
        return result.Distinct().OrderBy(p => p.ContainerPort).ToList();
    }

    private static ContainerState ParseState(string? s) => s?.ToLowerInvariant() switch
    {
        "created" => ContainerState.Created,
        "running" => ContainerState.Running,
        "paused" => ContainerState.Paused,
        "restarting" => ContainerState.Restarting,
        "exited" => ContainerState.Exited,
        "dead" => ContainerState.Dead,
        _ => ContainerState.Unknown
    };
}

/// <summary>Turns Docker's per-layer pull messages into one overall progress value.</summary>
internal sealed class PullProgressAggregator(string imageRef, IProgress<PullProgress>? target) : IProgress<JSONMessage>
{
    private readonly Dictionary<string, (long current, long total)> _layers = new();
    private readonly object _gate = new();

    public string? Error { get; private set; }

    public void Report(JSONMessage m)
    {
        if (!string.IsNullOrEmpty(m.ErrorMessage) || m.Error is not null)
        {
            Error = m.ErrorMessage ?? m.Error?.Message ?? "unknown error";
            return;
        }
        if (target is null) return;

        double? fraction = null;
        lock (_gate)
        {
            if (m.ID is not null && m.Progress is { Total: > 0 } p)
                _layers[m.ID] = (p.Current, p.Total);
            else if (m.ID is not null && m.Status is "Pull complete" or "Already exists" && _layers.TryGetValue(m.ID, out var l))
                _layers[m.ID] = (l.total, l.total);

            if (_layers.Count > 0)
            {
                var total = _layers.Values.Sum(v => v.total);
                if (total > 0) fraction = Math.Clamp((double)_layers.Values.Sum(v => v.current) / total, 0, 1);
            }
        }

        target.Report(new PullProgress(imageRef, m.Status ?? "", m.ID, m.Progress?.Current, m.Progress?.Total, fraction));
    }
}
