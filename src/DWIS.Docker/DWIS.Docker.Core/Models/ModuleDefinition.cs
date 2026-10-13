namespace DWIS.Docker.Core.Models;

/// <summary>Category of a predefined container. Drives grouping and the create rules.</summary>
public enum ModuleCategory
{
    Blackboard,
    Scheduler,
    Microstates,
    Composer,
    Bridge,
    Client,
    Advisor
}

public static class ModuleCategoryExtensions
{
    public static string DisplayName(this ModuleCategory category) => category switch
    {
        ModuleCategory.Blackboard => "Blackboards",
        ModuleCategory.Scheduler => "Scheduler",
        ModuleCategory.Microstates => "Microstates",
        ModuleCategory.Composer => "Composer",
        ModuleCategory.Bridge => "ADCS bridges",
        ModuleCategory.Client => "Clients",
        ModuleCategory.Advisor => "Advisors",
        _ => category.ToString()
    };
}

/// <summary>
/// Describes one predefined container: what image it runs, how its container is named,
/// where its configuration lives and which ports it exposes.
/// </summary>
/// <param name="Id">Stable slug, used as the <c>dwis.module</c> label.</param>
/// <param name="HostFolderName">Folder under the volumes root that is bind-mounted to <c>/home</c>. Null = no mount.</param>
/// <param name="ConfigType">POCO type of <c>config.json</c>, used for defaults and validation. Null = no editable config.</param>
/// <param name="ContainerPort">Port inside the container (48030 for blackboards, 8080 for web apps).</param>
/// <param name="HostPort">Port published on the host. Null = not published.</param>
public sealed record ModuleDefinition(
    string Id,
    string DisplayName,
    ModuleCategory Category,
    string ImageRepo,
    string ImageTag,
    string ContainerName,
    string? HostFolderName,
    Type? ConfigType,
    int? ContainerPort,
    int? HostPort)
{
    public string ImageRef => $"{ImageRepo}:{ImageTag}";
    public bool IsBlackboard => Category == ModuleCategory.Blackboard;
    public bool HasConfig => ConfigType is not null;
    public bool IsWebApp => Category is ModuleCategory.Client or ModuleCategory.Advisor;
}

/// <summary>Where blackboards replicate to.</summary>
public sealed record ReplicationTarget(string HubUrl, string Group, string? WritePassword);

/// <summary>Everything the create path needs besides the module definition.</summary>
/// <param name="BlackboardHostIp">Value of the BLACKBOARD_IP env var given to non-blackboard modules.</param>
/// <param name="HostVolumesRoot">Host-side root of the module folders, used as bind-mount source.</param>
/// <param name="Replication">Null = blackboards run without a hub.</param>
/// <param name="AddHostGatewayAlias">Add <c>host.docker.internal:host-gateway</c> (needed on Linux engines).</param>
public sealed record RunContext(
    string BlackboardHostIp,
    string HostVolumesRoot,
    ReplicationTarget? Replication,
    bool AddHostGatewayAlias);

/// <summary>Request to create a blackboard container from the Blackboards tab.</summary>
public sealed record BlackboardSpec(
    string ContainerName,
    int Port,
    string? HubId,
    string? Group,
    string? WritePassword,
    bool StartAfterCreate);
