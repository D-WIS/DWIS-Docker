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

        if (module.HostFolderName is not null)
        {
            if (string.IsNullOrWhiteSpace(ctx.HostVolumesRoot))
                throw new DwisDockerException("The host volumes root is not known. Set it in the settings before creating modules with a configuration folder.");
            var source = JoinHostPath(ctx.HostVolumesRoot, module.HostFolderName);
            p.HostConfig.Binds = [$"{source}:{ModuleCatalog.ContainerConfigPath}"];
        }

        if (module.ContainerPort is { } cp && module.HostPort is { } hp)
            AddPort(p, cp, hp);

        return p;
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
