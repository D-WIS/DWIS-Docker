namespace DWIS.Docker.Core.Models;

public enum ContainerState { Created, Running, Paused, Restarting, Exited, Dead, Unknown }

public enum ImageStatus { NotPulled, UpToDate, UpdateAvailable, Unknown }

/// <summary>One published port: container port mapped to a host port.</summary>
public sealed record PublishedPort(int ContainerPort, int HostPort);

public sealed record ContainerInfo(
    string Id,
    string Name,
    ContainerState State,
    string StatusText,
    string ImageRef,
    string ImageId,
    IReadOnlyDictionary<string, string> Labels,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PublishedPort>? Ports = null)
{
    public bool IsRunning => State == ContainerState.Running;
    public string ShortId => Id.Length > 12 ? Id[..12] : Id;
    public IReadOnlyList<PublishedPort> PublishedPorts => Ports ?? [];
}

public sealed record LocalImageInfo(
    string ImageId,
    IReadOnlyList<string> RepoTags,
    IReadOnlyList<string> RepoDigests,
    DateTimeOffset Created);

/// <summary>A blackboard container (from the catalog, created ad hoc, or left by the old app).</summary>
public sealed record BlackboardInfo(
    ContainerInfo Container,
    int Port,
    string? HubUrl,
    string? Group,
    bool IsLegacy)
{
    public bool IsReplicating => !string.IsNullOrEmpty(HubUrl);
}

/// <summary>One container of a module. Clients and advisors can have several, each bound to a blackboard.</summary>
/// <param name="Blackboard">The blackboard the container was created for (from its labels). Null for containers created without one.</param>
/// <param name="HostPort">Host port the container publishes, if any.</param>
/// <param name="IsLegacy">Created by a previous version (no labels), recognised by its image.</param>
public sealed record ModuleInstance(
    ContainerInfo Container,
    BlackboardTarget? Blackboard,
    int? HostPort,
    bool IsLegacy)
{
    public bool IsRunning => Container.IsRunning;
}

/// <param name="Container">The primary container of the module (the first of <paramref name="Instances"/>), kept for views that show one row per module.</param>
/// <param name="Instances">Every container of the module, oldest first. Several only for clients and advisors (or duplicates left by an older version).</param>
public sealed record ModuleStatus(
    ModuleDefinition Module,
    ContainerInfo? Container,
    bool ImagePresent,
    ImageStatus ImageStatus,
    bool? ConfigFileExists,
    BlackboardInfo? Blackboard,
    bool ReplicationMismatch,
    IReadOnlyList<ModuleInstance> Instances)
{
    public bool Exists => Container is not null;
    public bool IsRunning => Container?.IsRunning == true;
    public int DuplicateContainers => Math.Max(0, Instances.Count - 1);
}

public sealed record EngineStatus(bool Reachable, string? Version, string? OsType, string? Error)
{
    public static EngineStatus Unreachable(string error) => new(false, null, null, error);
}

/// <summary>An in-flight long operation, so every tab can show a spinner for the same row.</summary>
public sealed record ActiveOperation(string Key, string Description, double? Fraction);

public sealed record StatusSnapshot(
    DateTimeOffset TakenAt,
    EngineStatus Engine,
    IReadOnlyList<ModuleStatus> Modules,
    IReadOnlyList<BlackboardInfo> Blackboards,
    IReadOnlyList<RecordingInfo> Recordings,
    IReadOnlyList<LocalHubInfo> LocalHubs,
    IReadOnlyList<HubReplicationInfo> HubReplications,
    IReadOnlyList<ActiveOperation> Operations)
{
    public static StatusSnapshot Empty { get; } = new(
        DateTimeOffset.MinValue,
        new EngineStatus(false, null, null, "Not refreshed yet"),
        [],
        [],
        [],
        [],
        [],
        []);

    public LocalHubInfo? LocalHub(string hubId) => LocalHubs.FirstOrDefault(h => h.HubId == hubId);

    public ModuleStatus? Find(string moduleId) => Modules.FirstOrDefault(m => m.Module.Id == moduleId);

    public bool IsBusy(string key) => Operations.Any(o => o.Key == key);
}

public sealed record PullProgress(
    string ImageRef,
    string Status,
    string? LayerId,
    long? Current,
    long? Total,
    double? OverallFraction)
{
    public override string ToString() =>
        OverallFraction is { } f ? $"{Status} {f:P0}" : Status;
}

public sealed record OperationFailure(string Key, string Message);

public enum BulkAction { Start, Stop, Restart, Remove }
