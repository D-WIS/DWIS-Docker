using DWIS.Docker.Core.Models;
using DWIS.Docker.Core.Services;
using DWIS.Docker.UI.Services;
using DWIS.Docker.UI.Shared;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DWIS.Docker.UI.Components;

/// <summary>
/// Base for every live component: re-renders when the snapshot, settings, hubs or the
/// action runner change, and unsubscribes on dispose.
/// </summary>
public abstract class DwisComponentBase : ComponentBase, IDisposable
{
    private bool _disposed;

    [Inject] protected IStatusService Status { get; set; } = default!;
    [Inject] protected ISettingsStore Settings { get; set; } = default!;
    [Inject] protected IHubService Hubs { get; set; } = default!;
    [Inject] protected IModuleManager Modules { get; set; } = default!;
    [Inject] protected UiActionRunner Actions { get; set; } = default!;
    [Inject] protected IDialogService Dialogs { get; set; } = default!;
    [Inject] protected IPlatformServices Platform { get; set; } = default!;

    protected StatusSnapshot Snapshot => Status.Current;
    protected AppSettings AppSettings => Settings.Current;
    protected bool IsBusy => Actions.IsBusy;

    protected override void OnInitialized()
    {
        Status.Changed += OnSnapshotChanged;
        Settings.Changed += OnSettingsChanged;
        Hubs.Changed += OnHubsChanged;
        Actions.Changed += Rerender;
    }

    private void OnSnapshotChanged(object? sender, StatusSnapshot snapshot) => Rerender();
    private void OnSettingsChanged(object? sender, AppSettings settings) => Rerender();
    private void OnHubsChanged(object? sender, EventArgs e) => Rerender();

    protected void Rerender()
    {
        if (_disposed) return;
        try
        {
            _ = InvokeAsync(StateHasChanged);
        }
        catch (ObjectDisposedException)
        {
            // circuit gone
        }
        catch (InvalidOperationException)
        {
            // renderer disposed
        }
    }

    protected Task<bool> RunAsync(Func<CancellationToken, Task> action, string? successMessage = null, string? busyMessage = null, string? busyKey = null) =>
        Actions.RunAsync(action, successMessage, busyMessage, busyKey);

    protected Task<bool> ConfirmAsync(string title, string message, string confirmText = "Delete", Color color = Color.Error) =>
        ConfirmDialog.ShowAsync(Dialogs, title, message, confirmText, color);

    protected async Task CopyAsync(string text, string what = "Copied")
    {
        var ok = await Platform.CopyToClipboardAsync(text);
        Actions.Notify(ok ? $"{what} to clipboard." : "Could not copy to the clipboard.", ok ? Severity.Success : Severity.Warning);
    }

    /// <summary>Builds a <c>docker run</c> command (errors become snackbars) and copies it.</summary>
    protected async Task CopyRunCommandAsync(Func<string?> build)
    {
        var command = await Actions.QueryAsync(_ => Task.FromResult(build()));
        if (command is not null) await CopyAsync(command, "Command copied");
    }

    public virtual void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Status.Changed -= OnSnapshotChanged;
        Settings.Changed -= OnSettingsChanged;
        Hubs.Changed -= OnHubsChanged;
        Actions.Changed -= Rerender;
    }
}
