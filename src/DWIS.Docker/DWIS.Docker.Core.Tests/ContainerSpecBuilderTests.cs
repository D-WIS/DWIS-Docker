using DWIS.Docker.Core.Catalog;
using DWIS.Docker.Core.Models;
using DWIS.Docker.Core.Services;
using Xunit;

namespace DWIS.Docker.Core.Tests;

public class ContainerSpecBuilderTests
{
    private static readonly ReplicationTarget Norce = new("https://dwis.digiwells.no/blackboard/applications", "default", "secret");
    private static readonly RunContext Replicating = new("host.docker.internal", @"C:\Volumes", Norce, AddHostGatewayAlias: true);
    private static readonly RunContext Local = new("host.docker.internal", "/run/desktop/mnt/host/c/Volumes", null, AddHostGatewayAlias: false);

    [Fact]
    public void Blackboard_with_replication_gets_hub_command_labels_and_port()
    {
        var p = ContainerSpecBuilder.Build(ModuleCatalog.Blackboard, Replicating);

        Assert.Equal("blackboard-default", p.Name);
        Assert.Equal("digiwells/ddhubserver:latest", p.Image);
        Assert.Equal("localhost", p.Hostname);
        Assert.Equal(["--useHub", "--hubURL", Norce.HubUrl, "--hubGroup", "default", "--port", "48030", "--hubPWD", "secret"], p.Cmd);
        Assert.Equal("blackboard", p.Labels[ContainerSpecBuilder.LabelKind]);
        Assert.Equal("blackboard", p.Labels[ContainerSpecBuilder.LabelModule]);
        Assert.Equal(Norce.HubUrl, p.Labels[ContainerSpecBuilder.LabelHub]);
        Assert.Equal("default", p.Labels[ContainerSpecBuilder.LabelGroup]);
        Assert.Equal("48030", p.Labels[ContainerSpecBuilder.LabelPort]);
        Assert.Equal("48030", p.HostConfig.PortBindings["48030/tcp"][0].HostPort);
        Assert.Contains("host.docker.internal:host-gateway", p.HostConfig.ExtraHosts);
        Assert.Null(p.Env);
        Assert.Null(p.HostConfig.Binds);
    }

    [Fact]
    public void Blackboard_without_replication_only_sets_port()
    {
        var p = ContainerSpecBuilder.Build(ModuleCatalog.Blackboard, Local);

        Assert.Equal("blackboard", p.Name);
        Assert.Equal(["--port", "48030"], p.Cmd);
        Assert.False(p.Labels.ContainsKey(ContainerSpecBuilder.LabelHub));
        Assert.Null(p.HostConfig.ExtraHosts);
    }

    [Fact]
    public void Service_with_config_gets_env_and_host_side_bind()
    {
        var p = ContainerSpecBuilder.Build(ModuleCatalog.Composer, Replicating);

        Assert.Equal("advicecomposer", p.Name);
        Assert.True(p.Tty);
        Assert.True(p.OpenStdin);
        Assert.Equal(["BLACKBOARD_IP=host.docker.internal"], p.Env);
        Assert.Equal([@"C:\Volumes\DWISAdviceComposerService:/home"], p.HostConfig.Binds);
        Assert.Equal("service", p.Labels[ContainerSpecBuilder.LabelKind]);
        Assert.Null(p.ExposedPorts);
    }

    [Fact]
    public void Bind_source_uses_linux_separators_for_linux_roots()
    {
        var p = ContainerSpecBuilder.Build(ModuleCatalog.Composer, Local);
        Assert.Equal(["/run/desktop/mnt/host/c/Volumes/DWISAdviceComposerService:/home"], p.HostConfig.Binds);
    }

    [Fact]
    public void Service_with_config_but_unknown_root_throws()
    {
        var ctx = Local with { HostVolumesRoot = "" };
        Assert.Throws<DwisDockerException>(() => ContainerSpecBuilder.Build(ModuleCatalog.Composer, ctx));
    }

    [Fact]
    public void Web_app_publishes_host_port()
    {
        var logView = ModuleCatalog.Clients.Single(c => c.Id == "client-logview");
        var p = ContainerSpecBuilder.Build(logView, Local);

        Assert.Equal("webapp", p.Labels[ContainerSpecBuilder.LabelKind]);
        Assert.Equal("5276", p.HostConfig.PortBindings["8080/tcp"][0].HostPort);
        Assert.Equal("5276", p.Labels[ContainerSpecBuilder.LabelPort]);
        Assert.Null(p.HostConfig.Binds);
    }

    [Fact]
    public void Advisor_without_host_port_publishes_nothing()
    {
        var wob = ModuleCatalog.Advisors.Single(a => a.Id == "advisor-wob-correction");
        var p = ContainerSpecBuilder.Build(wob, Local);
        Assert.Null(p.HostConfig.PortBindings);
        Assert.Equal("", p.Labels[ContainerSpecBuilder.LabelPort]);
    }

    [Fact]
    public void Legacy_blackboard_command_is_parsed()
    {
        var (useHub, url, group, port) = ContainerSpecBuilder.ParseBlackboardCommand(
            ["--useHub", "--hubURL", Norce.HubUrl, "--hubGroup", "rig1", "--port", "48031"]);

        Assert.True(useHub);
        Assert.Equal(Norce.HubUrl, url);
        Assert.Equal("rig1", group);
        Assert.Equal(48031, port);
    }

    [Fact]
    public void Legacy_local_blackboard_command_is_parsed()
    {
        var (useHub, url, group, port) = ContainerSpecBuilder.ParseBlackboardCommand(["--port", "48030"]);
        Assert.False(useHub);
        Assert.Null(url);
        Assert.Null(group);
        Assert.Equal(48030, port);
    }

    [Fact]
    public void Legacy_container_is_matched_by_image()
    {
        var c = Container("digiwells/dwisschedulerservice-hsm:stable", new Dictionary<string, string>());
        Assert.Equal("scheduler", ContainerSpecBuilder.ResolveModuleId(c));
        Assert.False(ContainerSpecBuilder.IsManaged(c));
    }

    [Fact]
    public void Labelled_container_is_matched_by_label_even_with_other_image()
    {
        var c = Container("something/else:1", new Dictionary<string, string>
        {
            [ContainerSpecBuilder.LabelManaged] = "true",
            [ContainerSpecBuilder.LabelModule] = "composer"
        });
        Assert.Equal("composer", ContainerSpecBuilder.ResolveModuleId(c));
        Assert.True(ContainerSpecBuilder.IsManaged(c));
    }

    [Fact]
    public void Blackboard_info_from_labels_is_not_legacy()
    {
        var c = Container("digiwells/ddhubserver:latest", new Dictionary<string, string>
        {
            [ContainerSpecBuilder.LabelManaged] = "true",
            [ContainerSpecBuilder.LabelKind] = "blackboard",
            [ContainerSpecBuilder.LabelPort] = "48032",
            [ContainerSpecBuilder.LabelHub] = Norce.HubUrl,
            [ContainerSpecBuilder.LabelGroup] = "default"
        });
        var info = ContainerSpecBuilder.ToBlackboardInfo(c, null);

        Assert.False(info.IsLegacy);
        Assert.True(info.IsReplicating);
        Assert.Equal(48032, info.Port);
        Assert.Equal("default", info.Group);
    }

    [Fact]
    public void Blackboard_info_from_command_is_legacy()
    {
        var c = Container("digiwells/ddhubserver:latest", new Dictionary<string, string>());
        var info = ContainerSpecBuilder.ToBlackboardInfo(c, ["--useHub", "--hubURL", Norce.HubUrl, "--hubGroup", "g", "--port", "48030"]);

        Assert.True(info.IsLegacy);
        Assert.True(info.IsReplicating);
        Assert.Equal("g", info.Group);
    }

    private const string CaFile = @"C:\repos\D-WIS\Replay-Yggdsrasil\docker\tls\norce-chain.pem";
    private const string CaBind = CaFile + ":/certs/extra-ca.pem:ro";
    private const string CaEnv = "SSL_CERT_FILE=/certs/extra-ca.pem";

    [Fact]
    public void Replicating_blackboard_with_ca_file_gets_mount_and_env()
    {
        var p = ContainerSpecBuilder.Build(ModuleCatalog.Blackboard, Replicating with { CaCertificateFile = CaFile });

        Assert.Equal([CaBind], p.HostConfig.Binds);
        Assert.Equal([CaEnv], p.Env);
        // The rest of the spec is unchanged.
        Assert.Equal(["--useHub", "--hubURL", Norce.HubUrl, "--hubGroup", "default", "--port", "48030", "--hubPWD", "secret"], p.Cmd);
        Assert.Contains("host.docker.internal:host-gateway", p.HostConfig.ExtraHosts);
    }

    [Fact]
    public void Blackboard_without_replication_ignores_ca_file()
    {
        var p = ContainerSpecBuilder.Build(ModuleCatalog.Blackboard, Local with { CaCertificateFile = CaFile });
        Assert.Null(p.HostConfig.Binds);
        Assert.Null(p.Env);
    }

    [Fact]
    public void Blackboard_without_ca_file_has_no_mount_or_env()
    {
        foreach (var ca in new[] { null, "", "  " })
        {
            var p = ContainerSpecBuilder.Build(ModuleCatalog.Blackboard, Replicating with { CaCertificateFile = ca });
            Assert.Null(p.HostConfig.Binds);
            Assert.Null(p.Env);
        }
    }

    [Fact]
    public void Non_blackboard_module_does_not_get_ca_file()
    {
        var p = ContainerSpecBuilder.Build(ModuleCatalog.Composer, Replicating with { CaCertificateFile = CaFile });
        Assert.Equal(["BLACKBOARD_IP=host.docker.internal"], p.Env);
        Assert.Equal([@"C:\Volumes\DWISAdviceComposerService:/home"], p.HostConfig.Binds);
    }

    [Fact]
    public void Recorder_and_player_keep_recording_bind_and_add_ca_file()
    {
        var r = ContainerSpecBuilder.BuildRecorder("rec1", "digiwells/dwis-blackboard-recorder:latest", Norce.HubUrl, "default", @"C:\Volumes\Recordings\rec1", true, CaFile);
        Assert.Equal([@"C:\Volumes\Recordings\rec1:/recording", CaBind], r.HostConfig.Binds);
        Assert.Equal([CaEnv], r.Env);

        var pl = ContainerSpecBuilder.BuildPlayer("rec1", "digiwells/dwis-blackboard-player:latest", Norce.HubUrl, "default", @"C:\Volumes\Recordings\rec1", false, CaFile);
        Assert.Equal([@"C:\Volumes\Recordings\rec1:/recording", CaBind], pl.HostConfig.Binds);
        Assert.Equal([CaEnv], pl.Env);

        var plain = ContainerSpecBuilder.BuildRecorder("rec1", "digiwells/dwis-blackboard-recorder:latest", Norce.HubUrl, "default", @"C:\Volumes\Recordings\rec1", true);
        Assert.Equal([@"C:\Volumes\Recordings\rec1:/recording"], plain.HostConfig.Binds);
        Assert.Null(plain.Env);
    }

    [Fact]
    public void Local_hub_and_hub_replication_get_ca_file()
    {
        var hubSpec = new LocalHubSpec("rig", 8081, "/blackboard/applications", [], []);
        var hub = ContainerSpecBuilder.BuildLocalHub(hubSpec, "digiwells/dwissignalr:latest", "local-rig", true, CaFile);
        Assert.Equal([CaBind], hub.HostConfig.Binds);
        Assert.Equal([CaEnv], hub.Env);
        Assert.Equal("8081", hub.HostConfig.PortBindings["8080/tcp"][0].HostPort);

        var repSpec = new HubReplicationSpec("copy", [new ReplicationRouteInfo("http://a/hub", "g1", "https://b/hub", "g2")]);
        var rep = ContainerSpecBuilder.BuildHubReplication(repSpec, "digiwells/dwishubreplication:latest", false, CaFile);
        Assert.Equal([CaBind], rep.HostConfig.Binds);
        Assert.Equal([CaEnv], rep.Env);

        Assert.Null(ContainerSpecBuilder.BuildLocalHub(hubSpec, "digiwells/dwissignalr:latest", "local-rig", true).HostConfig.Binds);
        Assert.Null(ContainerSpecBuilder.BuildHubReplication(repSpec, "digiwells/dwishubreplication:latest", false).Env);
    }

    [Fact]
    public void Image_repo_strips_tag_and_digest()
    {
        Assert.Equal("digiwells/ddhubserver", ContainerSpecBuilder.ImageRepo("digiwells/ddhubserver:latest"));
        Assert.Equal("digiwells/ddhubserver", ContainerSpecBuilder.ImageRepo("digiwells/ddhubserver@sha256:abc"));
        Assert.Equal("localhost:5000/x", ContainerSpecBuilder.ImageRepo("localhost:5000/x:1"));
    }

    private static ContainerInfo Container(string image, Dictionary<string, string> labels) =>
        new("abc123", "name", ContainerState.Exited, "Exited", image, "sha256:img", labels, DateTimeOffset.UtcNow);
}
