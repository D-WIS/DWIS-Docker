using DWIS.Docker.Core.Models;
using MudBlazor;

namespace DWIS.Docker.UI;

/// <summary>How a state is drawn: icon, colour and label. One mapping for every tab.</summary>
public sealed record StateLook(string Icon, Color Color, string Text, string? Tooltip = null);

public static class DwisStates
{
    public static StateLook Container(ContainerInfo? c) => c switch
    {
        null => new(DwisIcons.Missing, Color.Default, "Missing", "No container exists yet"),
        { State: ContainerState.Running } => new(DwisIcons.Running, Color.Success, "Running", c.StatusText),
        { State: ContainerState.Restarting } => new(DwisIcons.Connecting, Color.Info, "Restarting", c.StatusText),
        { State: ContainerState.Paused } => new(DwisIcons.Stopped, Color.Warning, "Paused", c.StatusText),
        { State: ContainerState.Created } => new(DwisIcons.Stopped, Color.Warning, "Created", "Created, never started"),
        { State: ContainerState.Dead } => new(DwisIcons.Error, Color.Error, "Dead", c.StatusText),
        _ => new(DwisIcons.Stopped, Color.Warning, "Stopped", c.StatusText)
    };

    public static StateLook Image(ImageStatus s, bool present) => s switch
    {
        ImageStatus.UpToDate => new(DwisIcons.UpToDate, Color.Success, "Up to date", "Local image matches the registry"),
        ImageStatus.UpdateAvailable => new(DwisIcons.UpdateAvailable, Color.Warning, "Update available", "The registry has a newer image"),
        ImageStatus.NotPulled => new(DwisIcons.NotPulled, Color.Default, "Not pulled", "Image is not on this machine yet"),
        _ => present
            ? new(DwisIcons.Unknown, Color.Default, "Present", "Registry not checked yet")
            : new(DwisIcons.Unknown, Color.Default, "Unknown", "Registry not checked yet")
    };

    public static StateLook Config(bool? exists) => exists switch
    {
        null => new(DwisIcons.Config, Color.Default, "No config", "This module has no configuration file"),
        true => new(DwisIcons.Config, Color.Success, "Config", "config.json exists"),
        false => new(DwisIcons.ConfigMissing, Color.Warning, "No file", "config.json has not been created yet")
    };

    public static StateLook Hub(HubConnectionStatus s) => s switch
    {
        HubConnectionStatus.Connected => new(DwisIcons.Connected, Color.Success, "Connected"),
        HubConnectionStatus.Connecting => new(DwisIcons.Connecting, Color.Info, "Connecting"),
        HubConnectionStatus.Reconnecting => new(DwisIcons.Connecting, Color.Warning, "Reconnecting"),
        HubConnectionStatus.Failed => new(DwisIcons.Error, Color.Error, "Failed"),
        _ => new(DwisIcons.Disconnected, Color.Default, "Disconnected")
    };

    public static Color ActionColor(string icon) => icon switch
    {
        DwisIcons.Start => Color.Success,
        DwisIcons.Stop => Color.Error,
        DwisIcons.Restart => Color.Warning,
        DwisIcons.Create => Color.Primary,
        DwisIcons.Update or DwisIcons.Pull => Color.Info,
        DwisIcons.ClearManifests => Color.Warning,
        _ => Color.Default
    };
}
