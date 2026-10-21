using DWIS.Docker.Core.Catalog;
using DWIS.Docker.Core.Models;
using DWIS.Docker.Core.Services;
using Xunit;

namespace DWIS.Docker.Core.Tests;

/// <summary>Clients and advisors bound to a chosen blackboard: how they are created and recognised.</summary>
public class ModuleInstanceTests
{
    private static readonly RunContext Local = new("host.docker.internal", @"C:\Volumes", null, AddHostGatewayAlias: false);
    private static readonly BlackboardTarget Rig1 = new("blackboard-rig1", 48031);
    private static readonly ModuleDefinition LogView = ModuleCatalog.Clients.Single(c => c.Id == "client-logview");

    [Fact]
    public void Instance_gets_blackboard_env_vars_labels_name_and_port()
    {
        var module = ModuleCatalog.Instance(LogView, ContainerSpecBuilder.InstanceContainerName(LogView, Rig1.ContainerName), 5290);
        var p = ContainerSpecBuilder.Build(module, Local with { Blackboard = Rig1 });

        Assert.Equal("logview-blackboard-rig1", p.Name);
        Assert.Equal("digiwells/dwis-logview:stable", p.Image);
        Assert.Equal(["BLACKBOARD_IP=host.docker.internal", "BLACKBOARD_PORT=48031"], p.Env);
        Assert.Equal("client-logview", p.Labels[ContainerSpecBuilder.LabelModule]);
        Assert.Equal("webapp", p.Labels[ContainerSpecBuilder.LabelKind]);
        Assert.Equal("blackboard-rig1", p.Labels[ContainerSpecBuilder.LabelBlackboard]);
        Assert.Equal("48031", p.Labels[ContainerSpecBuilder.LabelBlackboardPort]);
        Assert.Equal("5290", p.Labels[ContainerSpecBuilder.LabelPort]);
        Assert.Equal("5290", p.HostConfig.PortBindings["8080/tcp"][0].HostPort);
    }

    [Fact]
    public void Instance_without_host_port_publishes_nothing_but_keeps_the_blackboard()
    {
        var wob = ModuleCatalog.Advisors.Single(a => a.Id == "advisor-wob-correction");
        var module = ModuleCatalog.Instance(wob, "wob-correction-blackboard-rig1", null);
        var p = ContainerSpecBuilder.Build(module, Local with { Blackboard = Rig1 });

        Assert.Null(p.HostConfig.PortBindings);
        Assert.Equal("", p.Labels[ContainerSpecBuilder.LabelPort]);
        Assert.Contains("BLACKBOARD_PORT=48031", p.Env);

        var c = Container(p.Name, p.Image, new Dictionary<string, string>(p.Labels));
        Assert.Null(ContainerSpecBuilder.ToModuleInstance(c, wob).HostPort);
    }

    [Fact]
    public void Module_without_blackboard_target_keeps_the_old_env()
    {
        var p = ContainerSpecBuilder.Build(LogView, Local);
        Assert.Equal(["BLACKBOARD_IP=host.docker.internal"], p.Env);
        Assert.False(p.Labels.ContainsKey(ContainerSpecBuilder.LabelBlackboard));
        Assert.Equal("5276", p.Labels[ContainerSpecBuilder.LabelPort]);
    }

    [Fact]
    public void Service_bound_to_a_blackboard_gets_the_port_too()
    {
        var p = ContainerSpecBuilder.Build(ModuleCatalog.Scheduler, Local with { Blackboard = Rig1 });
        Assert.Equal(["BLACKBOARD_IP=host.docker.internal", "BLACKBOARD_PORT=48031"], p.Env);
        Assert.Equal("service", p.Labels[ContainerSpecBuilder.LabelKind]);
    }

    [Fact]
    public void Instance_name_slugs_the_blackboard_name()
    {
        Assert.Equal("logview-my_board", ContainerSpecBuilder.InstanceContainerName(LogView, "my board"));
        Assert.Equal("logview-blackboard", ContainerSpecBuilder.InstanceContainerName(LogView, "blackboard"));
    }

    [Fact]
    public void Instance_is_recognised_from_labels()
    {
        var module = ModuleCatalog.Instance(LogView, "logview-blackboard-rig1", 5290);
        var p = ContainerSpecBuilder.Build(module, Local with { Blackboard = Rig1 });
        var c = Container(p.Name, p.Image, new Dictionary<string, string>(p.Labels));

        Assert.Equal("client-logview", ContainerSpecBuilder.ResolveModuleId(c));
        var instance = ContainerSpecBuilder.ToModuleInstance(c, LogView);

        Assert.False(instance.IsLegacy);
        Assert.Equal(Rig1, instance.Blackboard);
        Assert.Equal(5290, instance.HostPort);
    }

    [Fact]
    public void Published_port_from_docker_wins_over_the_label()
    {
        var c = Container("logview-x", "digiwells/dwis-logview:stable", new Dictionary<string, string>
        {
            [ContainerSpecBuilder.LabelManaged] = "true",
            [ContainerSpecBuilder.LabelModule] = "client-logview",
            [ContainerSpecBuilder.LabelPort] = "5290"
        }, [new PublishedPort(8080, 5291)]);

        Assert.Equal(5291, ContainerSpecBuilder.ToModuleInstance(c, LogView).HostPort);
    }

    [Fact]
    public void Labelled_container_without_blackboard_has_no_target()
    {
        var c = Container("logview", "digiwells/dwis-logview:stable", new Dictionary<string, string>
        {
            [ContainerSpecBuilder.LabelManaged] = "true",
            [ContainerSpecBuilder.LabelModule] = "client-logview"
        });
        var instance = ContainerSpecBuilder.ToModuleInstance(c, LogView);

        Assert.False(instance.IsLegacy);
        Assert.Null(instance.Blackboard);
        Assert.Equal(5276, instance.HostPort); // created before the port label existed: the module default
    }

    [Fact]
    public void Legacy_container_is_recognised_by_image_and_reports_the_default_port()
    {
        var c = Container("logview", "digiwells/dwis-logview:stable", new Dictionary<string, string>());

        Assert.Equal("client-logview", ContainerSpecBuilder.ResolveModuleId(c));
        var instance = ContainerSpecBuilder.ToModuleInstance(c, LogView);

        Assert.True(instance.IsLegacy);
        Assert.Null(instance.Blackboard);
        Assert.Equal(5276, instance.HostPort);
    }

    [Fact]
    public void Legacy_container_reports_the_port_docker_shows()
    {
        var c = Container("logview", "digiwells/dwis-logview:stable", new Dictionary<string, string>(), [new PublishedPort(8080, 6000)]);
        Assert.Equal(6000, ContainerSpecBuilder.ToModuleInstance(c, LogView).HostPort);
    }

    [Fact]
    public void Two_instances_of_one_module_resolve_to_the_same_module_id()
    {
        var a = ContainerSpecBuilder.Build(ModuleCatalog.Instance(LogView, "logview-blackboard", 5276), Local with { Blackboard = new BlackboardTarget("blackboard", 48030) });
        var b = ContainerSpecBuilder.Build(ModuleCatalog.Instance(LogView, "logview-blackboard-rig1", 5277), Local with { Blackboard = Rig1 });

        Assert.NotEqual(a.Name, b.Name);
        Assert.Equal(a.Labels[ContainerSpecBuilder.LabelModule], b.Labels[ContainerSpecBuilder.LabelModule]);
        Assert.Equal("48030", a.Labels[ContainerSpecBuilder.LabelBlackboardPort]);
        Assert.Equal("48031", b.Labels[ContainerSpecBuilder.LabelBlackboardPort]);
    }

    private static ContainerInfo Container(string name, string image, Dictionary<string, string> labels, IReadOnlyList<PublishedPort>? ports = null) =>
        new("abc123", name, ContainerState.Running, "Up", image, "sha256:img", labels, DateTimeOffset.UtcNow, ports);
}
