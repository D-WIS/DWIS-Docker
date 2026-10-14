using DWIS.Docker.Core.Models;
using DWIS.Docker.Core.Services;
using Xunit;

namespace DWIS.Docker.Core.Tests;

public class HubContainerTests
{
    private const string Norce = "https://dwis.digiwells.no/blackboard/applications";

    [Fact]
    public void Local_hub_spec_publishes_port_and_passes_source_and_destination_lists()
    {
        var spec = new LocalHubSpec("Rig hub", 5290, "/blackboard/applications",
            [new HubLink(Norce, "default", "default"), new HubLink(Norce, "rig1", "mirror")],
            [new HubLink("http://other:8080/x", "out", "out")]);

        var p = ContainerSpecBuilder.BuildLocalHub(spec, "digiwells/dwissignalr:latest", "local-rig_hub", addHostGatewayAlias: true);

        Assert.Equal("hub-Rig_hub", p.Name);
        Assert.Equal("digiwells/dwissignalr:latest", p.Image);
        Assert.Equal(
            ["--path", "/blackboard/applications",
             "--source-hubs", $"{Norce},default;{Norce},rig1,mirror",
             "--destination-hubs", "http://other:8080/x,out"],
            p.Cmd);
        Assert.Equal("5290", p.HostConfig.PortBindings["8080/tcp"][0].HostPort);
        Assert.Equal("hub", p.Labels[ContainerSpecBuilder.LabelKind]);
        Assert.Equal("local-rig_hub", p.Labels[ContainerSpecBuilder.LabelHubId]);
        Assert.Equal("5290", p.Labels[ContainerSpecBuilder.LabelPort]);
        Assert.Contains("host.docker.internal:host-gateway", p.HostConfig.ExtraHosts);
    }

    [Fact]
    public void Local_hub_without_links_only_passes_path_and_root_path_becomes_dot()
    {
        var p = ContainerSpecBuilder.BuildLocalHub(new LocalHubSpec("h", 5291, "", [], []), "img", "local-h", false);
        Assert.Equal(["--path", "."], p.Cmd);
        Assert.Equal("", p.Labels[ContainerSpecBuilder.LabelHubPath]);
    }

    [Fact]
    public void Local_hub_is_scanned_back_from_labels()
    {
        var spec = new LocalHubSpec("Rig hub", 5290, "blackboard/applications/", [new HubLink(Norce, "a", "b")], []);
        var p = ContainerSpecBuilder.BuildLocalHub(spec, "img", "local-rig_hub", false);
        var c = new ContainerInfo("id", p.Name, ContainerState.Running, "Up", "img", "sha", new Dictionary<string, string>(p.Labels), DateTimeOffset.UtcNow);

        var hubs = ContainerSpecBuilder.ScanLocalHubs([c]);

        var hub = Assert.Single(hubs);
        Assert.Equal("local-rig_hub", hub.HubId);
        Assert.Equal(5290, hub.HostPort);
        Assert.Equal("/blackboard/applications", hub.PathBase);
        Assert.Equal(new HubLink(Norce, "a", "b"), Assert.Single(hub.Sources));
        Assert.Empty(hub.Destinations);
    }

    [Fact]
    public void Replication_spec_passes_routes_and_is_scanned_back()
    {
        var routes = new[]
        {
            new ReplicationRouteInfo(Norce, "rig1", "http://host.docker.internal:5290/blackboard/applications", "rig1-mirror"),
            new ReplicationRouteInfo("http://a/h", "g1", "http://b/h", "g2")
        };
        var p = ContainerSpecBuilder.BuildHubReplication(new HubReplicationSpec("mirror", routes), "digiwells/dwishubreplication:latest", false);

        Assert.Equal("hub-replication-mirror", p.Name);
        Assert.Equal(["--routes", $"{Norce},rig1,http://host.docker.internal:5290/blackboard/applications,rig1-mirror;http://a/h,g1,http://b/h,g2"], p.Cmd);
        Assert.False(p.Tty);

        var c = new ContainerInfo("id", p.Name, ContainerState.Running, "Up", "img", "sha", new Dictionary<string, string>(p.Labels), DateTimeOffset.UtcNow);
        var rep = Assert.Single(ContainerSpecBuilder.ScanHubReplications([c]));
        Assert.Equal("mirror", rep.Name);
        Assert.Equal(routes, rep.Routes);
    }

    [Fact]
    public void Replication_without_routes_throws() =>
        Assert.Throws<DwisDockerException>(() => ContainerSpecBuilder.BuildHubReplication(new HubReplicationSpec("x", []), "img", false));

    [Fact]
    public void Hub_link_lists_round_trip_and_skip_broken_entries()
    {
        var parsed = ContainerSpecBuilder.ParseHubLinks($" {Norce} , default ; bad ; http://x/y,g1,g2 ");
        Assert.Equal([new HubLink(Norce, "default", "default"), new HubLink("http://x/y", "g1", "g2")], parsed);
        Assert.Equal($"{Norce},default;http://x/y,g1,g2", ContainerSpecBuilder.FormatHubLinks(parsed));
        Assert.Empty(ContainerSpecBuilder.ParseHubLinks(null));
    }

    [Fact]
    public void Path_is_normalised()
    {
        Assert.Equal("/blackboard/applications", ContainerSpecBuilder.NormalizePath("blackboard/applications/"));
        Assert.Equal("", ContainerSpecBuilder.NormalizePath("."));
        Assert.Equal("", ContainerSpecBuilder.NormalizePath(null));
    }
}
