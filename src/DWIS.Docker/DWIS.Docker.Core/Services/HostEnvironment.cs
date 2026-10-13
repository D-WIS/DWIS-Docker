using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace DWIS.Docker.Core.Services;

/// <summary>
/// Facts about where this process runs: inside the dwis-docker container or on a desktop,
/// where settings are stored, and where module config folders are on this side and on the host side.
/// </summary>
public sealed class HostEnvironment
{
    public const string DataDirEnvVar = "DWIS_DATA_DIR";
    public const string HostVolumesRootEnvVar = "DWIS_HOST_VOLUMES_ROOT";
    public const string ContainerVolumesMount = "/home";

    private readonly ILogger<HostEnvironment> _logger;
    private string? _detectedHostVolumesRoot;

    public HostEnvironment(DwisCoreOptions options, ILogger<HostEnvironment> logger)
    {
        _logger = logger;
        IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        IsInContainer = DetectContainer();
        DataDirectory = ResolveDataDirectory(options.DataDirectory);
        DockerHostEnv = Environment.GetEnvironmentVariable("DOCKER_HOST");
        _detectedHostVolumesRoot = Environment.GetEnvironmentVariable(HostVolumesRootEnvVar);
    }

    public bool IsWindows { get; }
    public bool IsInContainer { get; }

    /// <summary>Where settings.json lives.</summary>
    public string DataDirectory { get; }

    /// <summary>Raw DOCKER_HOST, if set.</summary>
    public string? DockerHostEnv { get; }

    /// <summary>Root under which THIS process reads and writes module config files.</summary>
    public string LocalVolumesRoot(string hostVolumesRoot) =>
        IsInContainer ? ContainerVolumesMount : hostVolumesRoot;

    /// <summary>
    /// Host-side root used as bind-mount source. On a desktop it is the local folder itself.
    /// Inside the container it is what the host mounted on /home, detected via <see cref="RecordDetectedHostVolumesRoot"/>.
    /// </summary>
    public string DefaultHostVolumesRoot
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_detectedHostVolumesRoot))
                return _detectedHostVolumesRoot;
            if (IsInContainer)
                return string.Empty;
            return IsWindows ? @"C:\Volumes" : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Volumes");
        }
    }

    public bool HostVolumesRootIsKnown => !string.IsNullOrWhiteSpace(DefaultHostVolumesRoot);

    /// <summary>Called by the Docker service after inspecting the app's own container.</summary>
    public void RecordDetectedHostVolumesRoot(string source)
    {
        if (string.IsNullOrWhiteSpace(source) || source == _detectedHostVolumesRoot) return;
        _detectedHostVolumesRoot = source;
        _logger.LogInformation("Detected host volumes root: {Root}", source);
    }

    /// <summary>Id of the container this process runs in, if any (from the hostname or cgroup).</summary>
    public string? OwnContainerIdHint()
    {
        if (!IsInContainer) return null;
        var host = Environment.MachineName;
        if (host.Length == 12 && host.All(Uri.IsHexDigit)) return host;
        try
        {
            foreach (var line in File.ReadLines("/proc/self/cgroup"))
            {
                var idx = line.LastIndexOf('/');
                if (idx < 0) continue;
                var tail = line[(idx + 1)..].Replace("docker-", "").Replace(".scope", "");
                if (tail.Length == 64 && tail.All(Uri.IsHexDigit)) return tail;
            }
        }
        catch
        {
            // not available, fall through
        }
        return host;
    }

    private static bool DetectContainer()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase))
            return true;
        return File.Exists("/.dockerenv");
    }

    private string ResolveDataDirectory(string? fromOptions)
    {
        var dir = fromOptions
                  ?? Environment.GetEnvironmentVariable(DataDirEnvVar);
        if (string.IsNullOrWhiteSpace(dir))
        {
            if (IsInContainer)
                dir = Path.Combine(ContainerVolumesMount, "dwis-docker");
            else if (IsWindows)
                dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DWIS.Docker");
            else
                dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "dwis-docker");
        }
        return Path.GetFullPath(dir);
    }
}
