using DWIS.Docker.Core.Models;

namespace DWIS.Docker.UI.Shared;

/// <summary>
/// One row of <see cref="ModuleTable"/>: a container of a module, or a placeholder for a module
/// that has none yet. Clients and advisors get one row per instance.
/// </summary>
public sealed record ModuleRow(ModuleStatus Status, ModuleInstance? Instance)
{
    public ModuleDefinition Module => Status.Module;
    public ContainerInfo? Container => Instance?.Container;
    public bool Exists => Instance is not null;
    public bool IsRunning => Instance?.IsRunning == true;

    /// <summary>Stable across snapshots: the container id, or the module id for a placeholder.</summary>
    public string Key => Instance?.Container.Id ?? Module.Id;

    /// <summary>Key the action runner and the operation tracker use for this row.</summary>
    public string BusyKey => Instance is null || Instance.Container.Id == Status.Container?.Id ? Module.Id : Instance.Container.Id;

    public static IEnumerable<ModuleRow> Expand(ModuleStatus status) =>
        status.Instances.Count == 0
            ? [new ModuleRow(status, null)]
            : status.Instances.Select(i => new ModuleRow(status, i));
}
