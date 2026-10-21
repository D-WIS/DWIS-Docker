using Docker.DotNet.Models;
using DWIS.Docker.Core.Catalog;
using DWIS.Docker.Core.Models;
using DWIS.Docker.Core.Services;
using Xunit;

namespace DWIS.Docker.Core.Tests;

public class DockerRunCommandFormatterTests
{
    private const string Norce = "https://dwis.digiwells.no/blackboard/applications";
    private static readonly RunContext Local = new("host.docker.internal", @"C:\Volumes", null, AddHostGatewayAlias: false);

    [Fact]
    public void Service_with_config_renders_name_tty_env_bind_labels_and_image()
    {
        var command = DockerRunCommandFormatter.Format(ContainerSpecBuilder.Build(ModuleCatalog.Composer, Local));

        Assert.Equal(
            "docker run -d --name advicecomposer -it -e BLACKBOARD_IP=host.docker.internal " +
            @"-v C:\Volumes\DWISAdviceComposerService:/home " +
            "-l dwis.kind=service -l dwis.managed=true -l dwis.module=composer digiwells/dwisadvicecomposerservice:stable",
            command);
    }

    [Fact]
    public void Blackboard_renders_hostname_port_labels_add_host_and_command_arguments()
    {
        var ctx = new RunContext("host.docker.internal", "/volumes", new ReplicationTarget(Norce, "rig 1", "p@ss word"), AddHostGatewayAlias: true);
        var command = DockerRunCommandFormatter.Format(ContainerSpecBuilder.Build(ModuleCatalog.Blackboard, ctx));

        Assert.StartsWith("docker run -d --name blackboard-rig_1 --hostname localhost -p 48030:48030 --add-host host.docker.internal:host-gateway ", command);
        Assert.Contains("-l \"dwis.group=rig 1\" -l dwis.hub=" + Norce + " -l dwis.kind=blackboard", command);
        Assert.EndsWith("digiwells/ddhubserver:latest --useHub --hubURL " + Norce + " --hubGroup \"rig 1\" --port 48030 --hubPWD \"p@ss word\"", command);
    }

    [Fact]
    public void Client_instance_renders_both_blackboard_variables_and_its_port()
    {
        var logView = ModuleCatalog.Clients.Single(c => c.Id == "client-logview");
        var module = ModuleCatalog.Instance(logView, "logview-blackboard-rig1", 5290);
        var command = DockerRunCommandFormatter.Format(ContainerSpecBuilder.Build(module, Local with { Blackboard = new BlackboardTarget("blackboard-rig1", 48031) }));

        Assert.Equal(
            "docker run -d --name logview-blackboard-rig1 -it -p 5290:8080 -e BLACKBOARD_IP=host.docker.internal -e BLACKBOARD_PORT=48031 " +
            "-l dwis.blackboard=blackboard-rig1 -l dwis.blackboard.port=48031 -l dwis.kind=webapp -l dwis.managed=true -l dwis.module=client-logview -l dwis.port=5290 " +
            "digiwells/dwis-logview:stable",
            command);
    }

    [Fact]
    public void Recorder_renders_bind_and_arguments()
    {
        var p = ContainerSpecBuilder.BuildRecorder("rig1-20260101", "digiwells/blackboardrecorder:latest", Norce, "rig1", @"C:\Volumes\Recordings\rig1-20260101", false);
        var command = DockerRunCommandFormatter.Format(p);

        Assert.StartsWith("docker run -d --name recorder-rig1-20260101 -it -v C:\\Volumes\\Recordings\\rig1-20260101:/recording ", command);
        Assert.EndsWith("digiwells/blackboardrecorder:latest --hubURL " + Norce + " --hubGroup rig1 --output-folder /recording", command);
    }

    [Fact]
    public void Local_hub_renders_published_port_and_hub_lists()
    {
        var spec = new LocalHubSpec("Rig hub", 5290, "/blackboard/applications", [new HubLink(Norce, "default", "mirror")], []);
        var command = DockerRunCommandFormatter.Format(ContainerSpecBuilder.BuildLocalHub(spec, "digiwells/dwissignalr:latest", "local-rig_hub", false));

        Assert.Contains("--name hub-Rig_hub -p 5290:8080 ", command);
        Assert.Contains("-l \"dwis.name=Rig hub\" ", command);
        Assert.EndsWith("digiwells/dwissignalr:latest --path /blackboard/applications --source-hubs " + Norce + ",default,mirror", command);
    }

    [Fact]
    public void Restart_policy_and_entrypoint_are_rendered_when_set()
    {
        var p = new CreateContainerParameters
        {
            Name = "x",
            Image = "img:1",
            Entrypoint = ["/bin/sh", "-c"],
            Cmd = ["echo hi"],
            HostConfig = new HostConfig { RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.UnlessStopped } }
        };
        Assert.Equal("docker run -d --name x --restart unless-stopped --entrypoint /bin/sh img:1 -c \"echo hi\"", DockerRunCommandFormatter.Format(p));
    }

    [Theory]
    [InlineData("plain-value_1.0", "plain-value_1.0")]
    [InlineData("C:\\Volumes\\X:/home", "C:\\Volumes\\X:/home")]
    [InlineData("has space", "\"has space\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData("a;b", "\"a;b\"")]
    [InlineData("", "\"\"")]
    public void Arguments_are_quoted_only_when_needed(string input, string expected) =>
        Assert.Equal(expected, DockerRunCommandFormatter.Quote(input));
}
