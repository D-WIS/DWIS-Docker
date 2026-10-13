namespace DWIS.Docker.Core.Models;

/// <summary>A SignalR blackboard hub the app can replicate to.</summary>
public sealed record HubDefinition(string Id, string Name, string BaseUrl, bool AutoConnect = true)
{
    public const string HubPath = "/dwishub";

    public string Endpoint => BaseUrl.TrimEnd('/') + HubPath;

    public static HubDefinition Norce { get; } =
        new("norce", "NORCE", "https://dwis.digiwells.no/blackboard/applications");

    public static string NewId() => Guid.NewGuid().ToString("N")[..8];
}

public enum HubConnectionStatus { Disconnected, Connecting, Connected, Reconnecting, Failed }

/// <param name="IsLocalOnly">Created in the UI but not yet seen on the hub.</param>
public sealed record HubGroupInfo(string Name, int ConnectionCount, bool IsLocalOnly);

public sealed record HubState(
    HubDefinition Hub,
    HubConnectionStatus Status,
    string? Error,
    IReadOnlyList<HubGroupInfo> Groups,
    DateTimeOffset? GroupsUpdatedAt)
{
    public bool IsConnected => Status == HubConnectionStatus.Connected;
}
