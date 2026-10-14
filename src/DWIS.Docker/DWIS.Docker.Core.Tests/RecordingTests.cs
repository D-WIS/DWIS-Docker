using DWIS.Docker.Core.Models;
using DWIS.Docker.Core.Services;
using Xunit;

namespace DWIS.Docker.Core.Tests;

public class RecordingTests
{
    private const string Hub = "https://dwis.digiwells.no/blackboard/applications";

    [Fact]
    public void Recorder_spec_mounts_folder_and_passes_hub_arguments()
    {
        var p = ContainerSpecBuilder.BuildRecorder("rig1-20261014-0900", "digiwells/blackboardrecorder:latest", Hub, "rig1", @"C:\Volumes\Recordings\rig1-20261014-0900", addHostGatewayAlias: false);

        Assert.Equal("recorder-rig1-20261014-0900", p.Name);
        Assert.Equal("digiwells/blackboardrecorder:latest", p.Image);
        Assert.True(p.Tty);
        Assert.True(p.OpenStdin);
        Assert.Equal(["--hubURL", Hub, "--hubGroup", "rig1", "--output-folder", "/recording"], p.Cmd);
        Assert.Equal([@"C:\Volumes\Recordings\rig1-20261014-0900:/recording"], p.HostConfig.Binds);
        Assert.Equal("recorder", p.Labels[ContainerSpecBuilder.LabelKind]);
        Assert.Equal("rig1-20261014-0900", p.Labels[ContainerSpecBuilder.LabelRecording]);
        Assert.Equal("rig1", p.Labels[ContainerSpecBuilder.LabelGroup]);
        Assert.Equal(Hub, p.Labels[ContainerSpecBuilder.LabelHub]);
    }

    [Fact]
    public void Player_spec_uses_input_folder()
    {
        var p = ContainerSpecBuilder.BuildPlayer("demo", "digiwells/blackboardplayer:latest", Hub, "replay", "/run/desktop/mnt/host/c/Volumes/Recordings/demo", addHostGatewayAlias: true);

        Assert.Equal("player-demo", p.Name);
        Assert.Equal(["--hubURL", Hub, "--hubGroup", "replay", "--input-folder", "/recording"], p.Cmd);
        Assert.Equal("player", p.Labels[ContainerSpecBuilder.LabelKind]);
        Assert.Contains("host.docker.internal:host-gateway", p.HostConfig.ExtraHosts);
    }

    [Fact]
    public void Recording_tool_without_host_root_throws() =>
        Assert.Throws<DwisDockerException>(() => ContainerSpecBuilder.BuildRecorder("x", "img", Hub, "g", "", false));

    [Fact]
    public void Recording_tool_is_recognised_from_labels()
    {
        var c = new ContainerInfo("id", "recorder-x", ContainerState.Running, "Up", "img", "sha", new Dictionary<string, string>
        {
            [ContainerSpecBuilder.LabelKind] = "recorder",
            [ContainerSpecBuilder.LabelRecording] = "x"
        }, DateTimeOffset.UtcNow);

        Assert.True(ContainerSpecBuilder.IsRecordingTool(c, out var kind, out var name));
        Assert.Equal("recorder", kind);
        Assert.Equal("x", name);
        Assert.False(ContainerSpecBuilder.LooksLikeBlackboard(c));
    }

    [Fact]
    public void Index_file_of_the_recorder_is_parsed()
    {
        const string json = """
            {"RootFolder":"/recording","DataInfos":[
              {"FileName":"rtData0","Start":"2026-10-14T08:00:00Z","End":"2026-10-14T08:05:00Z"},
              {"FileName":"rtData1","Start":"2026-10-14T08:05:00Z","End":"2026-10-14T08:09:30Z"}]}
            """;
        var index = RecordingScanner.ParseIndex(json);

        Assert.NotNull(index);
        Assert.Equal(2, index!.DataInfos!.Count);
        Assert.Equal("rtData1", index.DataInfos[1].FileName);
        Assert.Equal(TimeSpan.FromMinutes(9.5), index.DataInfos.Max(d => d.End) - index.DataInfos.Min(d => d.Start));
    }

    [Fact]
    public void Broken_index_file_gives_null() =>
        Assert.Null(RecordingScanner.ParseIndex("{ not json"));

    [Fact]
    public void Recording_state_follows_the_containers()
    {
        var running = new ContainerInfo("r", "recorder-x", ContainerState.Running, "Up", "img", "sha", new Dictionary<string, string>(), DateTimeOffset.UtcNow);
        var exited = running with { State = ContainerState.Exited };
        var info = new RecordingInfo("x", "h", "l", DateTimeOffset.UtcNow, 0, 0, null, null, null, null, null, null);

        Assert.Equal(RecordingState.Idle, info.State);
        Assert.Equal(RecordingState.Recording, (info with { Recorder = running }).State);
        Assert.Equal(RecordingState.Idle, (info with { Recorder = exited }).State);
        Assert.Equal(RecordingState.Replaying, (info with { Player = running }).State);
    }
}
