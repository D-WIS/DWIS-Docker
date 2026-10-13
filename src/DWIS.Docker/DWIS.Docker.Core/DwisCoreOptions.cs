namespace DWIS.Docker.Core;

/// <summary>Options for <see cref="ServiceCollectionExtensions.AddDwisDockerCore"/>.</summary>
public sealed class DwisCoreOptions
{
    /// <summary>Overrides the data directory (otherwise DWIS_DATA_DIR or a platform default).</summary>
    public string? DataDirectory { get; set; }

    /// <summary>False keeps settings in memory only (used by the updater).</summary>
    public bool PersistSettings { get; set; } = true;

    /// <summary>False skips the SignalR hub manager (used by the updater).</summary>
    public bool EnableHubs { get; set; } = true;

    /// <summary>False skips the background status monitor (used by the updater).</summary>
    public bool EnableMonitor { get; set; } = true;
}
