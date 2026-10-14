using System.Text.Json;
using Docker.DotNet.Models;
using DWIS.Docker.Core.Models;
using Microsoft.Extensions.Logging;

namespace DWIS.Docker.Core.Services;

/// <summary>Recordings of hub groups: one folder per recording, written by the recorder and replayed by the player.</summary>
public interface IRecordingManager
{
    /// <summary>Folder on the host that holds the recordings (bind-mount source).</summary>
    string HostRecordingsRoot { get; }

    /// <summary>Suggests a free recording name for a group.</summary>
    string SuggestName(string? group);

    Task StartRecordingAsync(RecordingSpec spec, IProgress<PullProgress>? progress = null, CancellationToken ct = default);
    Task StopRecordingAsync(string name, CancellationToken ct = default);
    Task StartReplayAsync(ReplaySpec spec, IProgress<PullProgress>? progress = null, CancellationToken ct = default);
    Task StopReplayAsync(string name, CancellationToken ct = default);

    /// <summary>Removes the recorder and player containers of the recording and deletes its folder.</summary>
    Task DeleteRecordingAsync(string name, CancellationToken ct = default);
}

/// <summary>Knows where recordings live and lists them. Used by the monitor (per poll) and by <see cref="RecordingService"/>.</summary>
public sealed class RecordingScanner(ISettingsStore settings, HostEnvironment env, ILogger<RecordingScanner> logger)
{
    /// <summary>Index file written by the recorder next to the chunk files.</summary>
    public const string IndexFileName = "rtData";

    private string HostRoot => string.IsNullOrWhiteSpace(settings.Current.HostVolumesRoot) ? env.DefaultHostVolumesRoot : settings.Current.HostVolumesRoot!;

    public string HostRecordingsRoot => ContainerSpecBuilder.JoinHostPath(HostRoot, settings.Current.RecordingsFolderName);
    public string LocalRecordingsRoot => Path.Combine(env.LocalVolumesRoot(HostRoot), settings.Current.RecordingsFolderName);
    public string HostFolder(string name) => ContainerSpecBuilder.JoinHostPath(HostRecordingsRoot, name);
    public string LocalFolder(string name) => Path.Combine(LocalRecordingsRoot, name);

    /// <summary>Lists the recording folders and matches them with recorder and player containers.</summary>
    public IReadOnlyList<RecordingInfo> Scan(IReadOnlyList<ContainerInfo> containers)
    {
        var recorders = new Dictionary<string, ContainerInfo>(StringComparer.OrdinalIgnoreCase);
        var players = new Dictionary<string, ContainerInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in containers)
        {
            if (!ContainerSpecBuilder.IsRecordingTool(c, out var kind, out var recording) || recording.Length == 0) continue;
            var target = kind == ContainerSpecBuilder.KindRecorder ? recorders : players;
            if (!target.TryGetValue(recording, out var existing) || existing.CreatedAt < c.CreatedAt)
                target[recording] = c;
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var root = LocalRecordingsRoot;
        try
        {
            if (Directory.Exists(root))
                foreach (var dir in Directory.EnumerateDirectories(root))
                    names.Add(Path.GetFileName(dir));
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Could not list {Root}", root);
        }
        foreach (var n in recorders.Keys) names.Add(n);
        foreach (var n in players.Keys) names.Add(n);

        var result = new List<RecordingInfo>();
        foreach (var name in names)
        {
            recorders.TryGetValue(name, out var recorder);
            players.TryGetValue(name, out var player);
            var local = LocalFolder(name);
            var (created, size, chunks, start, end) = ReadFolder(local);
            var labels = recorder?.Labels ?? player?.Labels;
            result.Add(new RecordingInfo(
                name, HostFolder(name), local, created, size, chunks, start, end,
                labels?.GetValueOrDefault(ContainerSpecBuilder.LabelHub),
                labels?.GetValueOrDefault(ContainerSpecBuilder.LabelGroup),
                recorder, player));
        }
        return result.OrderByDescending(r => r.CreatedAt).ToList();
    }

    private (DateTimeOffset created, long size, int chunks, DateTimeOffset? start, DateTimeOffset? end) ReadFolder(string folder)
    {
        if (!Directory.Exists(folder)) return (DateTimeOffset.MinValue, 0, 0, null, null);
        var info = new DirectoryInfo(folder);
        long size = 0;
        try
        {
            foreach (var f in info.EnumerateFiles()) size += f.Length;
        }
        catch (IOException) { }

        var index = TryReadIndex(Path.Combine(folder, IndexFileName));
        var chunks = index?.DataInfos?.Count ?? 0;
        DateTimeOffset? start = null, end = null;
        if (index?.DataInfos is { Count: > 0 } infos)
        {
            var s = infos.Min(d => d.Start);
            var e = infos.Max(d => d.End);
            if (s != DateTime.MaxValue) start = new DateTimeOffset(DateTime.SpecifyKind(s, DateTimeKind.Utc));
            if (e != DateTime.MinValue) end = new DateTimeOffset(DateTime.SpecifyKind(e, DateTimeKind.Utc));
        }
        return (info.CreationTimeUtc, size, chunks, start, end);
    }

    public static RecordMetaInfo? ParseIndex(string json)
    {
        try { return JsonSerializer.Deserialize<RecordMetaInfo>(json); }
        catch (JsonException) { return null; }
    }

    private RecordMetaInfo? TryReadIndex(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            return ParseIndex(File.ReadAllText(path));
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Could not read recording index {Path}", path);
            return null;
        }
    }

    // Shape of the index file written by DWIS.Server.Recording.Recorder (System.Text.Json, PascalCase).
    public sealed class RecordMetaInfo
    {
        public string? RootFolder { get; set; }
        public List<RecordDataInfo>? DataInfos { get; set; }
    }

    public sealed class RecordDataInfo
    {
        public string? FileName { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }
}

public sealed class RecordingService : IRecordingManager
{
    private readonly IDockerService _docker;
    private readonly ISettingsStore _settings;
    private readonly IStatusService _status;
    private readonly OperationTracker _operations;
    private readonly RecordingScanner _scanner;
    private readonly ILogger<RecordingService> _logger;

    public RecordingService(IDockerService docker, ISettingsStore settings, IStatusService status, OperationTracker operations, RecordingScanner scanner, ILogger<RecordingService> logger)
    {
        _docker = docker;
        _settings = settings;
        _status = status;
        _operations = operations;
        _scanner = scanner;
        _logger = logger;
    }

    public string HostRecordingsRoot => _scanner.HostRecordingsRoot;

    public string SuggestName(string? group)
    {
        var baseName = $"{Slug(string.IsNullOrWhiteSpace(group) ? "recording" : group)}-{DateTime.Now:yyyyMMdd-HHmm}";
        var existing = _status.Current.Recordings.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = baseName;
        for (var i = 2; existing.Contains(name); i++) name = $"{baseName}-{i}";
        return name;
    }

    // ---------------------------------------------------------------- actions

    public Task StartRecordingAsync(RecordingSpec spec, IProgress<PullProgress>? progress = null, CancellationToken ct = default)
    {
        var name = ValidName(spec.Name);
        var hub = ResolveHub(spec.HubId);
        var group = string.IsNullOrWhiteSpace(spec.Group) ? "default" : spec.Group.Trim();
        var existing = _status.Current.Recordings.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing?.Recorder is not null)
            throw new ContainerConflictException($"A recorder already exists for '{name}'. Stop it first.");
        if (existing?.HasData == true)
            throw new ContainerConflictException($"'{name}' already contains data. Choose another name.");

        return Track(name, $"Starting recording {name}", async token =>
        {
            var folder = _scanner.LocalFolder(name);
            var createdFolder = !Directory.Exists(folder);
            Directory.CreateDirectory(folder);
            try
            {
                var image = _settings.Current.RecorderImage;
                var p = ContainerSpecBuilder.BuildRecorder(name, image, hub.UrlForContainers, group, _scanner.HostFolder(name), LinuxEngine);
                var id = await CreateAsync(p, image, progress, token).ConfigureAwait(false);
                await _docker.StartAsync(id, token).ConfigureAwait(false);
            }
            catch when (createdFolder && IsEmpty(folder))
            {
                try { Directory.Delete(folder); } catch (IOException) { }
                throw;
            }
        }, ct);
    }

    public Task StopRecordingAsync(string name, CancellationToken ct = default) =>
        Track(name, $"Stopping recording {name}", async token =>
        {
            var rec = Find(name);
            if (rec.Recorder is null) throw new NotFoundException($"No recorder container for '{name}'.");
            await RemoveContainerAsync(rec.Recorder, token).ConfigureAwait(false);
        }, ct);

    public Task StartReplayAsync(ReplaySpec spec, IProgress<PullProgress>? progress = null, CancellationToken ct = default)
    {
        var name = ValidName(spec.Name);
        var hub = ResolveHub(spec.HubId);
        var group = string.IsNullOrWhiteSpace(spec.Group) ? "default" : spec.Group.Trim();
        var rec = Find(name);
        if (!rec.HasData) throw new DwisDockerException($"'{name}' has no recorded data yet.");
        if (rec.State == RecordingState.Recording) throw new ContainerConflictException($"'{name}' is still being recorded.");

        return Track(name, $"Replaying {name}", async token =>
        {
            if (rec.Player is not null)
                await RemoveContainerAsync(rec.Player, token).ConfigureAwait(false);
            var image = _settings.Current.PlayerImage;
            var p = ContainerSpecBuilder.BuildPlayer(name, image, hub.UrlForContainers, group, _scanner.HostFolder(name), LinuxEngine);
            var id = await CreateAsync(p, image, progress, token).ConfigureAwait(false);
            await _docker.StartAsync(id, token).ConfigureAwait(false);
        }, ct);
    }

    public Task StopReplayAsync(string name, CancellationToken ct = default) =>
        Track(name, $"Stopping replay of {name}", async token =>
        {
            var rec = Find(name);
            if (rec.Player is null) throw new NotFoundException($"No player container for '{name}'.");
            await RemoveContainerAsync(rec.Player, token).ConfigureAwait(false);
        }, ct);

    public Task DeleteRecordingAsync(string name, CancellationToken ct = default) =>
        Track(name, $"Deleting recording {name}", async token =>
        {
            var rec = Find(name);
            if (rec.Recorder is not null) await RemoveContainerAsync(rec.Recorder, token).ConfigureAwait(false);
            if (rec.Player is not null) await RemoveContainerAsync(rec.Player, token).ConfigureAwait(false);
            var folder = _scanner.LocalFolder(name);
            if (Directory.Exists(folder))
            {
                try { Directory.Delete(folder, recursive: true); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new DwisDockerException($"Could not delete the folder '{folder}': {ex.Message}", ex);
                }
            }
        }, ct);

    // ---------------------------------------------------------------- helpers

    private bool LinuxEngine => string.Equals(_status.Current.Engine.OsType, "linux", StringComparison.OrdinalIgnoreCase);

    private RecordingInfo Find(string name) =>
        _status.Current.Recordings.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? throw new NotFoundException($"Unknown recording '{name}'.");

    private HubDefinition ResolveHub(string hubId) =>
        _settings.Current.Hubs.FirstOrDefault(h => h.Id == hubId) ?? throw new DwisDockerException("Unknown hub.");

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
        using var _ = _operations.Begin("recording:" + key, description);
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

    private static bool IsEmpty(string folder)
    {
        try { return Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any(); }
        catch (IOException) { return false; }
    }

    private static string ValidName(string name)
    {
        name = name.Trim();
        if (name.Length == 0) throw new DwisDockerException("Recording name must not be empty.");
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name is "." or "..")
            throw new DwisDockerException("Recording name contains characters that are not allowed in a folder name.");
        return name;
    }

    private static string Slug(string s) =>
        new(s.Trim().Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_').ToArray());
}
