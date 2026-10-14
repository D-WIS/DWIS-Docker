namespace DWIS.Docker.Core.Models;

/// <summary>A SignalR blackboard hub the app can replicate to.</summary>
/// <param name="BaseUrl">Address this app connects to.</param>
/// <param name="ContainerBaseUrl">Address containers must use when it differs from <paramref name="BaseUrl"/> (a hub launched locally: localhost for the desktop app, host.docker.internal for containers).</param>
public sealed record HubDefinition(string Id, string Name, string BaseUrl, bool AutoConnect = true, string? ContainerBaseUrl = null)
{
    public const string HubPath = "/dwishub";
    public const string DefaultPath = "/blackboard/applications";

    public string Endpoint => BaseUrl.TrimEnd('/') + HubPath;

    /// <summary>The base URL to give containers (blackboards, recorders, replicators).</summary>
    public string UrlForContainers => string.IsNullOrWhiteSpace(ContainerBaseUrl) ? BaseUrl : ContainerBaseUrl;

    /// <summary>True when the URL is this hub, whichever of its two addresses is used.</summary>
    public bool Matches(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var u = url.TrimEnd('/');
        return string.Equals(u, BaseUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
            || string.Equals(u, UrlForContainers.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }

    public static HubDefinition Norce { get; } =
        new("norce", "NORCE", "https://dwis.digiwells.no/blackboard/applications");

    public static string NewId() => Guid.NewGuid().ToString("N")[..8];
}

public enum HubConnectionStatus { Disconnected, Connecting, Connected, Reconnecting, Failed }

/// <param name="IsLocalOnly">Created in the UI but not yet seen on the hub.</param>
public sealed record HubGroupInfo(string Name, int ConnectionCount, bool IsLocalOnly);

/// <summary>One remote hub group a local hub pulls from or pushes to (<c>--source-hubs</c> / <c>--destination-hubs</c>).</summary>
public sealed record HubLink(string Url, string RemoteGroup, string LocalGroup)
{
    public string Describe() => LocalGroup == RemoteGroup ? $"{Host(Url)} [{RemoteGroup}]" : $"{Host(Url)} [{RemoteGroup}] as [{LocalGroup}]";
    internal static string Host(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : url;
}

/// <summary>A hub server container started from this app.</summary>
public sealed record LocalHubInfo(
    string HubId,
    ContainerInfo Container,
    int HostPort,
    string PathBase,
    IReadOnlyList<HubLink> Sources,
    IReadOnlyList<HubLink> Destinations);

/// <summary>Request to start a hub server locally.</summary>
public sealed record LocalHubSpec(string Name, int HostPort, string PathBase, IReadOnlyList<HubLink> Sources, IReadOnlyList<HubLink> Destinations);

/// <summary>One direction of hub-to-hub replication done by the standalone replicator.</summary>
public sealed record ReplicationRouteInfo(string SourceUrl, string SourceGroup, string DestinationUrl, string DestinationGroup)
{
    public string Describe() => $"{HubLink.Host(SourceUrl)} [{SourceGroup}] → {HubLink.Host(DestinationUrl)} [{DestinationGroup}]";
}

/// <summary>A replicator container started from this app.</summary>
public sealed record HubReplicationInfo(string Name, ContainerInfo Container, IReadOnlyList<ReplicationRouteInfo> Routes);

public sealed record HubReplicationSpec(string Name, IReadOnlyList<ReplicationRouteInfo> Routes);

public sealed record HubState(
    HubDefinition Hub,
    HubConnectionStatus Status,
    string? Error,
    IReadOnlyList<HubGroupInfo> Groups,
    DateTimeOffset? GroupsUpdatedAt)
{
    public bool IsConnected => Status == HubConnectionStatus.Connected;
}
