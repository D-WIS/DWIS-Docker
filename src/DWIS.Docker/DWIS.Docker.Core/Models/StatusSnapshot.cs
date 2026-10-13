namespace DWIS.Docker.Core.Models;

public enum ContainerState { Created, Running, Paused, Restarting, Exited, Dead, Unknown }

public enum ImageStatus { NotPulled, UpToDate, UpdateAvailable, Unknown }

public sealed record ContainerInfo(
    string Id,
    string Name,
    ContainerState State,
    string StatusText,
    string ImageRef,
    string ImageId,
    IReadOnlyDictionary<string, string> Labels,
    DateTimeOffset CreatedAt)
{
    public bool IsRunning => State == ContainerState.Running;
    public string ShortId => Id.Length > 12 ? Id[..12] : Id;
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

public sealed record ModuleStatus(
    ModuleDefinition Module,
    ContainerInfo? Container,
    bool ImagePresent,
    ImageStatus ImageStatus,
    bool? ConfigFileExists,
    BlackboardInfo? Blackboard,
    bool ReplicationMismatch,
    int DuplicateContainers)
{
    public bool Exists => Container is not null;
    public bool IsRunning => Container?.IsRunning == true;
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
    IReadOnlyList<ActiveOperation> Operations)
{
    public static StatusSnapshot Empty { get; } = new(
        DateTimeOffset.MinValue,
        new EngineStatus(false, null, null, "Not refreshed yet"),
        [],
        [],
        []);

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
