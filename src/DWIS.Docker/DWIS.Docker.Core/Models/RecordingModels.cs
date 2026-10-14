namespace DWIS.Docker.Core.Models;

public enum RecordingState { Idle, Recording, Replaying }

/// <summary>A recording session: one folder of chunk files written by the recorder, plus the containers acting on it.</summary>
/// <param name="HostFolder">Folder on the host side (bind-mount source).</param>
/// <param name="LocalFolder">The same folder as seen by this process.</param>
/// <param name="Start">First timestamp in the recording, from the index file.</param>
/// <param name="End">Last timestamp in the recording, from the index file.</param>
/// <param name="HubUrl">Hub the recorder listened to (from the recorder container's labels, if any).</param>
public sealed record RecordingInfo(
    string Name,
    string HostFolder,
    string LocalFolder,
    DateTimeOffset CreatedAt,
    long SizeBytes,
    int ChunkCount,
    DateTimeOffset? Start,
    DateTimeOffset? End,
    string? HubUrl,
    string? Group,
    ContainerInfo? Recorder,
    ContainerInfo? Player)
{
    public bool HasData => ChunkCount > 0;
    public TimeSpan? Duration => Start is { } s && End is { } e && e > s ? e - s : null;

    public RecordingState State =>
        Recorder?.IsRunning == true ? RecordingState.Recording :
        Player?.IsRunning == true ? RecordingState.Replaying :
        RecordingState.Idle;
}

/// <summary>Request to start recording a hub group into a new folder.</summary>
public sealed record RecordingSpec(string Name, string HubId, string Group);

/// <summary>Request to replay a recording into a hub group.</summary>
public sealed record ReplaySpec(string Name, string HubId, string Group);
