using MudBlazor;

namespace DWIS.Docker.UI;

/// <summary>The one icon vocabulary used by every tab. Add here, never inline an icon in a component.</summary>
public static class DwisIcons
{
    // actions
    public const string Start = Icons.Material.Filled.PlayArrow;
    public const string Stop = Icons.Material.Filled.Stop;
    public const string Restart = Icons.Material.Filled.RestartAlt;
    public const string Delete = Icons.Material.Outlined.Delete;
    public const string Create = Icons.Material.Filled.AddBox;
    public const string Pull = Icons.Material.Filled.CloudDownload;
    public const string Update = Icons.Material.Filled.SystemUpdateAlt;
    public const string UpdateApp = Icons.Material.Filled.Upgrade;
    public const string Logs = Icons.Material.Outlined.Article;
    public const string Refresh = Icons.Material.Filled.Refresh;
    public const string Settings = Icons.Material.Outlined.Settings;
    public const string Save = Icons.Material.Filled.Save;
    public const string ResetDefaults = Icons.Material.Outlined.SettingsBackupRestore;
    public const string Format = Icons.Material.Outlined.FormatAlignLeft;
    public const string Copy = Icons.Material.Outlined.ContentCopy;
    public const string Add = Icons.Material.Filled.Add;
    public const string Remove = Icons.Material.Outlined.RemoveCircleOutline;
    public const string Cancel = Icons.Material.Filled.Close;
    public const string Check = Icons.Material.Filled.Check;
    public const string CheckUpdates = Icons.Material.Outlined.Radar;
    public const string Target = Icons.Material.Filled.GpsFixed;
    public const string ExpandMore = Icons.Material.Filled.ExpandMore;
    public const string ExpandLess = Icons.Material.Filled.ExpandLess;

    // config
    public const string Config = Icons.Material.Outlined.DataObject;
    public const string ConfigMissing = Icons.Material.Outlined.NoteAdd;

    // hubs and replication
    public const string Hub = Icons.Material.Filled.Hub;
    public const string Group = Icons.Material.Filled.Groups;
    public const string Port = Icons.Material.Outlined.OpenInNew;
    public const string Password = Icons.Material.Outlined.Key;
    public const string ClearManifests = Icons.Material.Outlined.CleaningServices;
    public const string Replication = Icons.Material.Filled.Sync;
    public const string ReplicationOff = Icons.Material.Filled.SyncDisabled;
    public const string Connected = Icons.Material.Filled.Link;
    public const string Connecting = Icons.Material.Filled.Autorenew;
    public const string Disconnected = Icons.Material.Filled.LinkOff;

    // container state
    public const string Running = Icons.Material.Filled.CheckCircle;
    public const string Stopped = Icons.Material.Filled.PauseCircle;
    public const string Missing = Icons.Material.Outlined.RadioButtonUnchecked;
    public const string Legacy = Icons.Material.Outlined.History;
    public const string Mismatch = Icons.Material.Outlined.ReportProblem;

    // image state
    public const string UpToDate = Icons.Material.Filled.Verified;
    public const string UpdateAvailable = Icons.Material.Filled.NewReleases;
    public const string NotPulled = Icons.Material.Outlined.CloudOff;
    public const string Unknown = Icons.Material.Outlined.HelpOutline;

    // recording and replay
    public const string Record = Icons.Material.Filled.FiberManualRecord;
    public const string Recording = Icons.Material.Filled.RadioButtonChecked;
    public const string Replay = Icons.Material.Filled.PlayCircle;
    public const string Replaying = Icons.Material.Filled.Autorenew;
    public const string Duration = Icons.Material.Outlined.Timer;
    public const string Storage = Icons.Material.Outlined.Storage;

    // misc
    public const string Error = Icons.Material.Filled.ErrorOutline;
    public const string Info = Icons.Material.Outlined.Info;
    public const string Docker = Icons.Material.Outlined.Dns;
    public const string Blackboard = Icons.Material.Outlined.Dashboard;
    public const string Folder = Icons.Material.Outlined.Folder;

    // tabs
    public const string TabReplication = Hub;
    public const string TabStandard = Icons.Material.Outlined.PlaylistAddCheck;
    public const string TabBlackboards = Blackboard;
    public const string TabRecording = Icons.Material.Outlined.Videocam;
    public const string TabModules = Icons.Material.Outlined.Extension;
    public const string TabClients = Icons.Material.Outlined.Devices;
    public const string TabAdvisors = Icons.Material.Outlined.Lightbulb;
}
