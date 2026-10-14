using System.Text.Json.Serialization;

namespace DWIS.Docker.Core.Models;

/// <summary>User settings, persisted as JSON by <see cref="Services.ISettingsStore"/>.</summary>
public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Docker API endpoint. Null = DOCKER_HOST env var if sane, else the platform default.</summary>
    public string? DockerEndpoint { get; set; }

    /// <summary>Host-side folder that holds the module folders. Null = auto-detected default.</summary>
    public string? HostVolumesRoot { get; set; }

    /// <summary>Value of BLACKBOARD_IP given to modules so they can reach the blackboard.</summary>
    public string BlackboardHostIp { get; set; } = "host.docker.internal";

    public List<HubDefinition> Hubs { get; set; } = [HubDefinition.Norce];

    public bool ReplicationEnabled { get; set; } = true;
    public string? ReplicationHubId { get; set; } = HubDefinition.Norce.Id;
    public string ReplicationGroup { get; set; } = "default";
    public string? ReplicationGroupPassword { get; set; }

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan RegistryCheckInterval { get; set; } = TimeSpan.FromMinutes(30);
    public int LogTailLines { get; set; } = 500;

    /// <summary>Image of the blackboard recorder (listens to a hub group and writes chunk files).</summary>
    public string RecorderImage { get; set; } = "digiwells/blackboardrecorder:latest";

    /// <summary>Image of the blackboard player (replays chunk files into a hub group).</summary>
    public string PlayerImage { get; set; } = "digiwells/blackboardplayer:latest";

    /// <summary>Sub-folder of the volumes root that holds one folder per recording.</summary>
    public string RecordingsFolderName { get; set; } = "Recordings";

    [JsonIgnore]
    public HubDefinition? ReplicationHub =>
        ReplicationHubId is null ? null : Hubs.FirstOrDefault(h => h.Id == ReplicationHubId);

    public AppSettings Clone() => new()
    {
        SchemaVersion = SchemaVersion,
        DockerEndpoint = DockerEndpoint,
        HostVolumesRoot = HostVolumesRoot,
        BlackboardHostIp = BlackboardHostIp,
        Hubs = Hubs.ToList(),
        ReplicationEnabled = ReplicationEnabled,
        ReplicationHubId = ReplicationHubId,
        ReplicationGroup = ReplicationGroup,
        ReplicationGroupPassword = ReplicationGroupPassword,
        PollingInterval = PollingInterval,
        RegistryCheckInterval = RegistryCheckInterval,
        LogTailLines = LogTailLines,
        RecorderImage = RecorderImage,
        PlayerImage = PlayerImage,
        RecordingsFolderName = RecordingsFolderName
    };

    /// <summary>Returns a list of problems; empty when the settings are usable.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (!string.IsNullOrWhiteSpace(DockerEndpoint) && !Uri.TryCreate(DockerEndpoint, UriKind.Absolute, out _))
            errors.Add($"Docker endpoint '{DockerEndpoint}' is not a valid URI.");

        if (string.IsNullOrWhiteSpace(BlackboardHostIp))
            errors.Add("Blackboard host IP must not be empty.");

        if (PollingInterval < TimeSpan.FromSeconds(1) || PollingInterval > TimeSpan.FromMinutes(5))
            errors.Add("Polling interval must be between 1 second and 5 minutes.");

        if (LogTailLines is < 10 or > 100_000)
            errors.Add("Log tail must be between 10 and 100000 lines.");

        if (string.IsNullOrWhiteSpace(RecorderImage) || RecorderImage.Any(char.IsWhiteSpace))
            errors.Add("Recorder image must be a valid image reference.");
        if (string.IsNullOrWhiteSpace(PlayerImage) || PlayerImage.Any(char.IsWhiteSpace))
            errors.Add("Player image must be a valid image reference.");
        if (string.IsNullOrWhiteSpace(RecordingsFolderName) || RecordingsFolderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            errors.Add("Recordings folder name must be a plain folder name.");

        var duplicateIds = Hubs.GroupBy(h => h.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateIds.Count > 0)
            errors.Add($"Duplicate hub ids: {string.Join(", ", duplicateIds)}.");

        foreach (var hub in Hubs)
        {
            if (string.IsNullOrWhiteSpace(hub.Name))
                errors.Add($"Hub '{hub.Id}' has no name.");
            if (!Uri.TryCreate(hub.BaseUrl, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                errors.Add($"Hub '{hub.Name}' has an invalid URL '{hub.BaseUrl}'.");
        }

        if (ReplicationEnabled)
        {
            if (ReplicationHub is null)
                errors.Add("Replication is enabled but no hub is selected.");
            if (string.IsNullOrWhiteSpace(ReplicationGroup))
                errors.Add("Replication group must not be empty.");
        }

        return errors;
    }
}
