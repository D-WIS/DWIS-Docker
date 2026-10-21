using Docker.DotNet.Models;
using DWIS.Docker.Core.Catalog;
using DWIS.Docker.Core.Models;

namespace DWIS.Docker.Core.Services;

/// <summary>
/// The single place that turns a module definition into Docker create parameters,
/// and that recognises containers (ours by label, the old app's by image and command).
/// Pure and unit-testable.
/// </summary>
internal static class ContainerSpecBuilder
{
    public const string LabelManaged = "dwis.managed";
    public const string LabelModule = "dwis.module";
    public const string LabelKind = "dwis.kind";
    public const string LabelPort = "dwis.port";
    public const string LabelHub = "dwis.hub";
    public const string LabelGroup = "dwis.group";
    /// <summary>Container name of the blackboard a client or advisor instance is bound to.</summary>
    public const string LabelBlackboard = "dwis.blackboard";
    public const string LabelBlackboardPort = "dwis.blackboard.port";

    public const string KindBlackboard = "blackboard";
    public const string KindService = "service";
    public const string KindWebApp = "webapp";
    public const string KindRecorder = "recorder";
    public const string KindPlayer = "player";

    /// <summary>Name of the recording a recorder or player container works on.</summary>
    public const string LabelRecording = "dwis.recording";
    public const string RecordingContainerPath = "/recording";

    public const string HostGatewayAlias = "host.docker.internal:host-gateway";

    public static string RecorderContainerName(string recording) => $"recorder-{Slug(recording)}";
    public static string PlayerContainerName(string recording) => $"player-{Slug(recording)}";

    /// <summary>Recorder: listens to a hub group and writes chunk files into the mounted recording folder.</summary>
    public static CreateContainerParameters BuildRecorder(string recording, string imageRef, string hubUrl, string group, string hostFolder, bool addHostGatewayAlias) =>
        BuildRecordingTool(KindRecorder, RecorderContainerName(recording), recording, imageRef, hubUrl, group, hostFolder, "--output-folder", addHostGatewayAlias);

    /// <summary>Player: replays the chunk files of the mounted recording folder into a hub group.</summary>
    public static CreateContainerParameters BuildPlayer(string recording, string imageRef, string hubUrl, string group, string hostFolder, bool addHostGatewayAlias) =>
        BuildRecordingTool(KindPlayer, PlayerContainerName(recording), recording, imageRef, hubUrl, group, hostFolder, "--input-folder", addHostGatewayAlias);

    private static CreateContainerParameters BuildRecordingTool(string kind, string name, string recording, string imageRef, string hubUrl, string group, string hostFolder, string folderOption, bool addHostGatewayAlias)
    {
        if (string.IsNullOrWhiteSpace(hostFolder))
            throw new DwisDockerException("The host volumes root is not known. Set it in the settings before recording or replaying.");
        var p = new CreateContainerParameters
        {
            Name = name,
            Image = imageRef,
            // Both tools wait on Console.ReadLine, so they need a tty or they exit at once.
            Tty = true,
            OpenStdin = true,
            Cmd = ["--hubURL", hubUrl, "--hubGroup", group, folderOption, RecordingContainerPath],
            Labels = new Dictionary<string, string>
            {
                [LabelManaged] = "true",
                [LabelModule] = kind,
                [LabelKind] = kind,
                [LabelHub] = hubUrl,
                [LabelGroup] = group,
                [LabelRecording] = recording
            },
            HostConfig = new HostConfig
            {
                Binds = [$"{hostFolder}:{RecordingContainerPath}"]
            }
        };
        if (addHostGatewayAlias)
            p.HostConfig.ExtraHosts = [HostGatewayAlias];
        return p;
    }

    // ---------------------------------------------------------------- local hub server and hub replicator

    public const string KindHub = "hub";
    public const string KindHubReplication = "hubreplication";
    public const string LabelHubId = "dwis.hub.id";
    public const string LabelHubPath = "dwis.hub.path";
    public const string LabelHubSources = "dwis.hub.sources";
    public const string LabelHubDestinations = "dwis.hub.destinations";
    public const string LabelName = "dwis.name";
    public const string LabelRoutes = "dwis.routes";
    public const int HubContainerPort = 8080;
    public const string DefaultHubPath = HubDefinition.DefaultPath;

    public static string LocalHubContainerName(string name) => $"hub-{Slug(name)}";
    public static string HubReplicationContainerName(string name) => $"hub-replication-{Slug(name)}";
    public static string LocalHubId(string name) => $"local-{Slug(name).ToLowerInvariant()}";

    /// <summary>The SignalR hub server (DWIS.Server.SignalRHub): <c>--path</c>, <c>--source-hubs</c>, <c>--destination-hubs</c>, port 8080.</summary>
    public static CreateContainerParameters BuildLocalHub(LocalHubSpec spec, string imageRef, string hubId, bool addHostGatewayAlias)
    {
        var path = NormalizePath(spec.PathBase);
        var cmd = new List<string> { "--path", path.Length == 0 ? "." : path };
        if (spec.Sources.Count > 0) cmd.AddRange(["--source-hubs", FormatHubLinks(spec.Sources)]);
        if (spec.Destinations.Count > 0) cmd.AddRange(["--destination-hubs", FormatHubLinks(spec.Destinations)]);

        var p = new CreateContainerParameters
        {
            Name = LocalHubContainerName(spec.Name),
            Image = imageRef,
            Cmd = cmd,
            Labels = new Dictionary<string, string>
            {
                [LabelManaged] = "true",
                [LabelModule] = KindHub,
                [LabelKind] = KindHub,
                [LabelHubId] = hubId,
                [LabelName] = spec.Name,
                [LabelPort] = spec.HostPort.ToString(),
                [LabelHubPath] = path,
                [LabelHubSources] = FormatHubLinks(spec.Sources),
                [LabelHubDestinations] = FormatHubLinks(spec.Destinations)
            },
            HostConfig = new HostConfig()
        };
        AddPort(p, HubContainerPort, spec.HostPort);
        if (addHostGatewayAlias) p.HostConfig.ExtraHosts = [HostGatewayAlias];
        return p;
    }

    /// <summary>The standalone replicator (DWIS.Hub.HubReplication): <c>--routes</c>.</summary>
    public static CreateContainerParameters BuildHubReplication(HubReplicationSpec spec, string imageRef, bool addHostGatewayAlias)
    {
        if (spec.Routes.Count == 0) throw new DwisDockerException("At least one route is needed.");
        var routes = FormatRoutes(spec.Routes);
        var p = new CreateContainerParameters
        {
            Name = HubReplicationContainerName(spec.Name),
            Image = imageRef,
            Cmd = ["--routes", routes],
            Labels = new Dictionary<string, string>
            {
                [LabelManaged] = "true",
                [LabelModule] = KindHubReplication,
                [LabelKind] = KindHubReplication,
                [LabelName] = spec.Name,
                [LabelRoutes] = routes
            },
            HostConfig = new HostConfig()
        };
        if (addHostGatewayAlias) p.HostConfig.ExtraHosts = [HostGatewayAlias];
        return p;
    }

    public static string FormatHubLinks(IEnumerable<HubLink> links) =>
        string.Join(";", links.Select(l => l.LocalGroup == l.RemoteGroup ? $"{l.Url},{l.RemoteGroup}" : $"{l.Url},{l.RemoteGroup},{l.LocalGroup}"));

    public static IReadOnlyList<HubLink> ParseHubLinks(string? raw)
    {
        var list = new List<HubLink>();
        foreach (var entry in (raw ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length is < 2 or > 3 || parts.Any(string.IsNullOrEmpty)) continue;
            list.Add(new HubLink(parts[0], parts[1], parts.Length == 3 ? parts[2] : parts[1]));
        }
        return list;
    }

    public static string FormatRoutes(IEnumerable<ReplicationRouteInfo> routes) =>
        string.Join(";", routes.Select(r => $"{r.SourceUrl},{r.SourceGroup},{r.DestinationUrl},{r.DestinationGroup}"));

    public static IReadOnlyList<ReplicationRouteInfo> ParseRoutes(string? raw)
    {
        var list = new List<ReplicationRouteInfo>();
        foreach (var entry in (raw ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != 4 || parts.Any(string.IsNullOrEmpty)) continue;
            list.Add(new ReplicationRouteInfo(parts[0], parts[1], parts[2], parts[3]));
        }
        return list;
    }

    /// <summary>Hub server containers started from this app, from their labels.</summary>
    public static IReadOnlyList<LocalHubInfo> ScanLocalHubs(IEnumerable<ContainerInfo> containers)
    {
        var result = new List<LocalHubInfo>();
        foreach (var c in containers)
        {
            if (!c.Labels.TryGetValue(LabelKind, out var kind) || kind != KindHub) continue;
            if (!c.Labels.TryGetValue(LabelHubId, out var hubId)) continue;
            var port = c.Labels.TryGetValue(LabelPort, out var ps) && int.TryParse(ps, out var p) ? p : HubContainerPort;
            result.Add(new LocalHubInfo(
                hubId, c, port,
                c.Labels.GetValueOrDefault(LabelHubPath, DefaultHubPath),
                ParseHubLinks(c.Labels.GetValueOrDefault(LabelHubSources)),
                ParseHubLinks(c.Labels.GetValueOrDefault(LabelHubDestinations))));
        }
        return result;
    }

    /// <summary>Replicator containers started from this app, from their labels.</summary>
    public static IReadOnlyList<HubReplicationInfo> ScanHubReplications(IEnumerable<ContainerInfo> containers)
    {
        var result = new List<HubReplicationInfo>();
        foreach (var c in containers)
        {
            if (!c.Labels.TryGetValue(LabelKind, out var kind) || kind != KindHubReplication) continue;
            result.Add(new HubReplicationInfo(
                c.Labels.GetValueOrDefault(LabelName, c.Name),
                c,
                ParseRoutes(c.Labels.GetValueOrDefault(LabelRoutes))));
        }
        return result.OrderBy(r => r.Name).ToList();
    }

    /// <summary>"/blackboard/applications" style: leading slash, no trailing slash; "" for the root.</summary>
    public static string NormalizePath(string? path)
    {
        var p = (path ?? "").Trim().Trim('/');
        return p.Length == 0 || p == "." ? "" : "/" + p;
    }

    public static bool IsRecordingTool(ContainerInfo c, out string kind, out string recording)
    {
        kind = "";
        recording = "";
        if (!c.Labels.TryGetValue(LabelKind, out var k) || (k != KindRecorder && k != KindPlayer)) return false;
        kind = k;
        recording = c.Labels.GetValueOrDefault(LabelRecording, "");
        return true;
    }

    public static string ContainerNameFor(ModuleDefinition module, RunContext ctx)
    {
        if (module.Id == ModuleCatalog.Blackboard.Id && ctx.Replication is { } r)
            return $"{module.ContainerName}-{Slug(r.Group)}";
        return module.ContainerName;
    }

    /// <summary>Default name of a client or advisor instance: the module's container name plus the blackboard's.</summary>
    public static string InstanceContainerName(ModuleDefinition module, string blackboardContainerName) =>
        $"{module.ContainerName}-{Slug(blackboardContainerName)}";

    public static CreateContainerParameters Build(ModuleDefinition module, RunContext ctx)
    {
        var labels = new Dictionary<string, string>
        {
            [LabelManaged] = "true",
            [LabelModule] = module.Id,
        };

        var p = new CreateContainerParameters
        {
            Name = ContainerNameFor(module, ctx),
            Image = module.ImageRef,
            Labels = labels,
            HostConfig = new HostConfig()
        };

        if (ctx.AddHostGatewayAlias)
            p.HostConfig.ExtraHosts = [HostGatewayAlias];

        if (module.IsBlackboard)
        {
            var port = module.ContainerPort ?? ModuleCatalog.DefaultBlackboardPort;
            labels[LabelKind] = KindBlackboard;
            labels[LabelPort] = port.ToString();
            p.Hostname = "localhost";

            var cmd = new List<string>();
            if (ctx.Replication is { } rep)
            {
                labels[LabelHub] = rep.HubUrl;
                labels[LabelGroup] = rep.Group;
                cmd.AddRange(["--useHub", "--hubURL", rep.HubUrl, "--hubGroup", rep.Group]);
            }
            cmd.AddRange(["--port", port.ToString()]);
            if (ctx.Replication?.WritePassword is { Length: > 0 } pwd)
                cmd.AddRange(["--hubPWD", pwd]);
            p.Cmd = cmd;

            AddPort(p, port, module.HostPort ?? port);
            return p;
        }

        labels[LabelKind] = module.IsWebApp ? KindWebApp : KindService;
        p.Tty = true;
        p.OpenStdin = true;
        p.Env = [$"{ModuleCatalog.BlackboardIpEnvVar}={ctx.BlackboardHostIp}"];
        if (ctx.Blackboard is { } target)
        {
            p.Env.Add($"{ModuleCatalog.BlackboardPortEnvVar}={target.Port}");
            labels[LabelBlackboard] = target.ContainerName;
            labels[LabelBlackboardPort] = target.Port.ToString();
        }

        if (module.HostFolderName is not null)
        {
            if (string.IsNullOrWhiteSpace(ctx.HostVolumesRoot))
                throw new DwisDockerException("The host volumes root is not known. Set it in the settings before creating modules with a configuration folder.");
            var source = JoinHostPath(ctx.HostVolumesRoot, module.HostFolderName);
            p.HostConfig.Binds = [$"{source}:{ModuleCatalog.ContainerConfigPath}"];
        }

        if (module.ContainerPort is { } cp && module.HostPort is { } hp)
            AddPort(p, cp, hp);
        // Web apps record the host port (empty = not published) so stopped containers, which Docker
        // lists without ports, still show it.
        if (module.IsWebApp)
            labels[LabelPort] = module.HostPort?.ToString() ?? "";

        return p;
    }

    /// <summary>
    /// Instance details of a non-blackboard container: the blackboard it was bound to and the port it
    /// publishes, from our labels; a container without labels is legacy and reports what Docker shows.
    /// </summary>
    public static ModuleInstance ToModuleInstance(ContainerInfo c, ModuleDefinition module)
    {
        BlackboardTarget? target = null;
        if (c.Labels.TryGetValue(LabelBlackboard, out var bb) && bb.Length > 0)
        {
            var port = c.Labels.TryGetValue(LabelBlackboardPort, out var ps) && int.TryParse(ps, out var pp) ? pp : ModuleCatalog.DefaultBlackboardPort;
            target = new BlackboardTarget(bb, port);
        }

        int? hostPort = null;
        if (module.ContainerPort is { } cp && c.PublishedPorts.FirstOrDefault(p => p.ContainerPort == cp) is { } published)
            hostPort = published.HostPort;
        else if (c.Labels.TryGetValue(LabelPort, out var hs))
            hostPort = int.TryParse(hs, out var hp) ? hp : null;
        else
            hostPort = module.HostPort; // legacy, or created by this app before the port label existed

        return new ModuleInstance(c, target, hostPort, IsLegacy: !IsManaged(c));
    }

    /// <summary>Module id this container belongs to, from our label or (legacy) from its image.</summary>
    public static string? ResolveModuleId(ContainerInfo c)
    {
        if (c.Labels.TryGetValue(LabelModule, out var id))
            return id;
        var match = ModuleCatalog.All.FirstOrDefault(m => ImageMatches(c.ImageRef, m));
        return match?.Id;
    }

    public static bool IsManaged(ContainerInfo c) => c.Labels.ContainsKey(LabelManaged);

    public static bool LooksLikeBlackboard(ContainerInfo c) =>
        c.Labels.TryGetValue(LabelKind, out var kind) ? kind == KindBlackboard
                                                      : ImageRepo(c.ImageRef) == ModuleCatalog.BlackboardImageRepo;

    /// <summary>Blackboard details from our labels, or by parsing the command of a container created by the old app.</summary>
    public static BlackboardInfo ToBlackboardInfo(ContainerInfo c, IList<string>? cmd)
    {
        if (c.Labels.TryGetValue(LabelKind, out var kind) && kind == KindBlackboard)
        {
            var port = c.Labels.TryGetValue(LabelPort, out var ps) && int.TryParse(ps, out var pp) ? pp : ModuleCatalog.DefaultBlackboardPort;
            c.Labels.TryGetValue(LabelHub, out var hub);
            c.Labels.TryGetValue(LabelGroup, out var group);
            return new BlackboardInfo(c, port, hub, group, IsLegacy: false);
        }

        var (useHub, hubUrl, hubGroup, legacyPort) = ParseBlackboardCommand(cmd);
        return new BlackboardInfo(c, legacyPort, useHub ? hubUrl : null, useHub ? hubGroup : null, IsLegacy: true);
    }

    public static (bool useHub, string? hubUrl, string? group, int port) ParseBlackboardCommand(IList<string>? cmd)
    {
        var useHub = false;
        string? hubUrl = null;
        string? group = null;
        var port = ModuleCatalog.DefaultBlackboardPort;
        if (cmd is null) return (useHub, hubUrl, group, port);

        for (var i = 0; i < cmd.Count; i++)
        {
            var next = i + 1 < cmd.Count ? cmd[i + 1] : null;
            switch (cmd[i])
            {
                case "--useHub":
                    useHub = next is null || !next.Equals("false", StringComparison.OrdinalIgnoreCase);
                    break;
                case "--hubURL": hubUrl = next; break;
                case "--hubGroup": group = next; break;
                case "--port":
                    if (next is not null && int.TryParse(next, out var p)) port = p;
                    break;
            }
        }
        if (useHub && group is null) group = "default";
        return (useHub, hubUrl, group, port);
    }

    private static void AddPort(CreateContainerParameters p, int containerPort, int hostPort)
    {
        var key = $"{containerPort}/tcp";
        p.ExposedPorts ??= new Dictionary<string, EmptyStruct>();
        p.ExposedPorts[key] = default;
        p.HostConfig.PortBindings ??= new Dictionary<string, IList<PortBinding>>();
        p.HostConfig.PortBindings[key] = [new PortBinding { HostPort = hostPort.ToString() }];
    }

    private static bool ImageMatches(string imageRef, ModuleDefinition m) =>
        string.Equals(ImageRepo(imageRef), m.ImageRepo, StringComparison.OrdinalIgnoreCase);

    public static string ImageRepo(string imageRef)
    {
        var at = imageRef.IndexOf('@');
        if (at >= 0) imageRef = imageRef[..at];
        var slash = imageRef.LastIndexOf('/');
        var colon = imageRef.LastIndexOf(':');
        return colon > slash ? imageRef[..colon] : imageRef;
    }

    public static string JoinHostPath(string root, string folder)
    {
        var separator = root.Contains('\\') || (root.Length >= 2 && root[1] == ':') ? '\\' : '/';
        return root.TrimEnd('\\', '/') + separator + folder;
    }

    private static string Slug(string s) =>
        new(s.Trim().Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_').ToArray());
}
