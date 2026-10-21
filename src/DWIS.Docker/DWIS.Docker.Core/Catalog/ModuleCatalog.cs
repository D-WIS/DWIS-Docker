using DWIS.Docker.Core.Models;

namespace DWIS.Docker.Core.Catalog;

/// <summary>
/// The predefined containers the app manages. This is the single source of truth for
/// image names, tags, container names, config folders and ports.
/// </summary>
public static class ModuleCatalog
{
    public const string Namespace = "digiwells";

    /// <summary>Env var every non-blackboard module reads to find the blackboard (same literal as the DWIS client library).</summary>
    public const string BlackboardIpEnvVar = "BLACKBOARD_IP";
    /// <summary>Env var clients and advisors read for the blackboard's OPC UA port (same literal as the DWIS client library).</summary>
    public const string BlackboardPortEnvVar = "BLACKBOARD_PORT";
    public const string DefaultBlackboardHostIp = "host.docker.internal";
    public const int DefaultBlackboardPort = 48030;
    public const int WebAppContainerPort = 8080;
    public const string ConfigFileName = "config.json";
    public const string ContainerConfigPath = "/home";

    public const string AppImageRef = "digiwells/dwis-docker:stable";
    public const string UpdaterImageRef = "digiwells/dwis-app-update:stable";
    public const string BlackboardImageRepo = "digiwells/ddhubserver";

    private const string Stable = "stable";
    private const string Latest = "latest";

    public static ModuleDefinition Blackboard { get; } = new(
        "blackboard", "Default Blackboard", ModuleCategory.Blackboard,
        BlackboardImageRepo, Latest, "blackboard",
        HostFolderName: null, ConfigType: null,
        ContainerPort: DefaultBlackboardPort, HostPort: DefaultBlackboardPort);

    public static ModuleDefinition Scheduler { get; } = new(
        "scheduler", "Scheduler", ModuleCategory.Scheduler,
        "digiwells/dwisschedulerservice-hsm", Stable, "schedulerservice",
        HostFolderName: null, ConfigType: null, ContainerPort: null, HostPort: null);

    public static ModuleDefinition MicrostateEngine { get; } = new(
        "microstate-engine", "Microstate engine", ModuleCategory.Microstates,
        "digiwells/dwismicrostateinterpretationengine", Stable, "microstatesengine",
        "DWISMicrostateInterpretationEngineService", typeof(MicroStateInterpretationConfiguration),
        ContainerPort: null, HostPort: null);

    public static ModuleDefinition MicrostateThresholds { get; } = new(
        "microstate-thresholds", "Microstate thresholds", ModuleCategory.Microstates,
        "digiwells/dwismicrostatethresholdsserver", Stable, "microstatethresholds",
        "DWISMicroStateSignalGenerator", typeof(MicroStateThresholdsConfiguration),
        ContainerPort: null, HostPort: null);

    public static ModuleDefinition Composer { get; } = new(
        "composer", "Advice composer", ModuleCategory.Composer,
        "digiwells/dwisadvicecomposerservice", Stable, "advicecomposer",
        "DWISAdviceComposerService", typeof(ComposerConfiguration),
        ContainerPort: null, HostPort: null);

    public static IReadOnlyList<ModuleDefinition> Bridges { get; } =
    [
        Bridge("autodriller", "Auto-driller", "AutoDriller", typeof(AutoDrillerConfiguration)),
        Bridge("circulationstartup", "Circulation startup", "CirculationStartup", typeof(CirculationStartupConfiguration)),
        Bridge("circulationstop", "Circulation stop", "CirculationStop", typeof(CirculationStopConfiguration)),
        Bridge("frictiontest", "Friction test", "FrictionTest", typeof(FrictionTestConfiguration)),
        Bridge("maxflowratesoe", "Max flowrate SOE", "MaxFlowrateSOE", null),
        Bridge("overpressurefdir", "Over-pressure FDIR", "OverPressureFDIR", null),
        Bridge("overtorquefdir", "Over-torque FDIR", "OverTorqueFDIR", null),
        Bridge("overpullunderpullfdir", "Overpull / underpull FDIR", "OverpullUnderpullFDIR", null),
        Bridge("pickoffbottom", "Pick off-bottom", "PickOffBottom", typeof(PickOffBottomConfiguration)),
        Bridge("reciprocation", "Reciprocation", "Reciprocation", typeof(ReciprocationConfiguration)),
        Bridge("rotationstartup", "Rotation startup", "RotationStartup", typeof(RotationStartupConfiguration)),
        Bridge("rotationstop", "Rotation stop", "RotationStop", typeof(RotationStopConfiguration)),
        Bridge("swabsurgesoe", "Swab-surge SOE", "SwabSurgeSOE", null),
        Bridge("tagbottom", "Tag bottom", "TagBottom", typeof(TagBottomConfiguration)),
    ];

    public static IReadOnlyList<ModuleDefinition> Clients { get; } =
    [
        WebApp(ModuleCategory.Client, "logview", "Log view", "digiwells/dwis-logview", 5276),
        WebApp(ModuleCategory.Client, "driller-scheduling", "Driller scheduling", "digiwells/driller-scheduling-app", 5274),
        WebApp(ModuleCategory.Client, "advisor-scheduling", "Advisor scheduling", "digiwells/advisor-scheduling-app", 5275),
        WebApp(ModuleCategory.Client, "blackboard-explorer", "Blackboard explorer", "digiwells/dwis-blackboard-explorer", 5277),
    ];

    public static IReadOnlyList<ModuleDefinition> Advisors { get; } =
    [
        WebApp(ModuleCategory.Advisor, "wob-correction", "WOB correction", "digiwells/dwisservicewobcorrectionsserver", null),
        WebApp(ModuleCategory.Advisor, "downhole-ecd", "Downhole ECD", "digiwells/dwisservicedownholeecdserver", null),
        WebApp(ModuleCategory.Advisor, "drillstring", "Drill-string", "digiwells/dwiscontextualdatabridgebhadrillstringserver", 5278),
        WebApp(ModuleCategory.Advisor, "trajectory", "Trajectory", "digiwells/dwiscontextualdatabridgetrajectoryserver", 5279),
        WebApp(ModuleCategory.Advisor, "wellbore-architecture", "Wellbore architecture", "digiwells/dwiscontextualdatabridgewellborearchitectureserver", 5280),
        WebApp(ModuleCategory.Advisor, "wellbore-selector", "Wellbore selector", "digiwells/dwiscontextualdatawellboreselectorwebapp", 5281),
    ];

    /// <summary>What the Standard setup tab lists by default.</summary>
    public static IReadOnlyList<ModuleDefinition> StandardSetup { get; } =
        [Blackboard, Scheduler, MicrostateEngine, MicrostateThresholds, Composer, .. Bridges];

    public static IReadOnlyList<ModuleDefinition> All { get; } =
        [.. StandardSetup, .. Clients, .. Advisors];

    private static readonly Dictionary<string, ModuleDefinition> ById = All.ToDictionary(m => m.Id);

    public static ModuleDefinition? Find(string id) => ById.GetValueOrDefault(id);

    public static IEnumerable<ModuleDefinition> OfCategory(ModuleCategory category) =>
        All.Where(m => m.Category == category);

    /// <summary>Definition for a blackboard created from the Blackboards tab (not the catalog one).</summary>
    public static ModuleDefinition AdHocBlackboard(string containerName, int port) =>
        Blackboard with
        {
            Id = AdHocBlackboardId,
            DisplayName = containerName,
            ContainerName = containerName,
            ContainerPort = port,
            HostPort = port
        };

    public const string AdHocBlackboardId = "blackboard-adhoc";

    /// <summary>Definition for one instance of a client or advisor: same module id, its own container name and host port.</summary>
    public static ModuleDefinition Instance(ModuleDefinition module, string containerName, int? hostPort) =>
        module with { ContainerName = containerName, HostPort = hostPort };

    private static ModuleDefinition Bridge(string slug, string displayName, string folderSuffix, Type? configType) => new(
        $"bridge-{slug}", displayName, ModuleCategory.Bridge,
        $"digiwells/dwisadcsbridgegeneric{slug}", Stable, $"adcsgeneric{slug}",
        $"DWISADCSBridgeGeneric{folderSuffix}", configType,
        ContainerPort: null, HostPort: null);

    private static ModuleDefinition WebApp(ModuleCategory category, string slug, string displayName, string repo, int? hostPort) => new(
        $"{category.ToString().ToLowerInvariant()}-{slug}", displayName, category,
        repo, Stable, slug,
        HostFolderName: null, ConfigType: null,
        ContainerPort: WebAppContainerPort, HostPort: hostPort);
}
