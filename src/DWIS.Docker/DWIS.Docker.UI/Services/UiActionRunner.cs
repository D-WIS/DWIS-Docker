using DWIS.Docker.Core.Models;
using DWIS.Docker.Core.Services;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace DWIS.Docker.UI.Services;

/// <summary>
/// Runs user actions for one circuit: owns the busy state, pull progress, error snackbars
/// and the refresh afterwards, so components never contain try/catch boilerplate.
/// </summary>
public sealed class UiActionRunner : IDisposable
{
    private readonly ISnackbar _snackbar;
    private readonly IStatusService _status;
    private readonly ILogger<UiActionRunner> _logger;
    private CancellationTokenSource? _cts;
    private DateTime _lastProgressPublish;

    public UiActionRunner(ISnackbar snackbar, IStatusService status, ILogger<UiActionRunner> logger)
    {
        _snackbar = snackbar;
        _status = status;
        _logger = logger;
        Progress = new Progress<PullProgress>(OnProgress);
    }

    public bool IsBusy { get; private set; }
    public string? BusyMessage { get; private set; }
    public string? BusyKey { get; private set; }
    public string? ProgressText { get; private set; }
    public double? ProgressFraction { get; private set; }

    /// <summary>Pass to Core calls that pull images; the latest line shows in the busy bar.</summary>
    public IProgress<PullProgress> Progress { get; }

    public event Action? Changed;

    public bool IsBusyWith(string key) => IsBusy && BusyKey == key;

    /// <summary>Runs one action. Returns true on success. Errors become snackbars.</summary>
    public async Task<bool> RunAsync(
        Func<CancellationToken, Task> action,
        string? successMessage = null,
        string? busyMessage = null,
        string? busyKey = null,
        bool refreshAfter = true)
    {
        if (!Begin(busyMessage, busyKey)) return false;
        try
        {
            await action(_cts!.Token);
            if (successMessage is not null) _snackbar.Add(successMessage, Severity.Success);
            return true;
        }
        catch (OperationCanceledException)
        {
            _snackbar.Add("Cancelled.", Severity.Info);
            return false;
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return false;
        }
        finally
        {
            await End(refreshAfter);
        }
    }

    /// <summary>Runs several steps, continuing after failures, and reports one summary.</summary>
    public async Task<int> RunManyAsync(
        IReadOnlyList<(string label, Func<CancellationToken, Task> step)> steps,
        string successMessage,
        string? busyKey = null)
    {
        if (steps.Count == 0) return 0;
        if (!Begin(steps[0].label, busyKey)) return 0;
        var failures = new List<string>();
        var done = 0;
        try
        {
            for (var i = 0; i < steps.Count; i++)
            {
                var (label, step) = steps[i];
                BusyMessage = steps.Count > 1 ? $"{i + 1}/{steps.Count} {label}" : label;
                Changed?.Invoke();
                try
                {
                    await step(_cts!.Token);
                    done++;
                }
                catch (OperationCanceledException)
                {
                    failures.Add("Cancelled.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Step '{Label}' failed", label);
                    failures.Add($"{label}: {Message(ex)}");
                }
            }
        }
        finally
        {
            await End(true);
        }

        if (failures.Count == 0)
            _snackbar.Add(successMessage, Severity.Success);
        else
            _snackbar.Add($"{done}/{steps.Count} succeeded. " + string.Join(" ", failures), Severity.Error, o => o.RequireInteraction = true);
        return done;
    }

    /// <summary>Read-only call with error reporting but no busy state or refresh.</summary>
    public async Task<T?> QueryAsync<T>(Func<CancellationToken, Task<T>> query, CancellationToken ct = default)
    {
        try
        {
            return await query(ct);
        }
        catch (OperationCanceledException)
        {
            return default;
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return default;
        }
    }

    public void ReportFailures(IReadOnlyList<OperationFailure> failures, string successMessage)
    {
        if (failures.Count == 0)
            _snackbar.Add(successMessage, Severity.Success);
        else
            _snackbar.Add(string.Join(" ", failures.Select(f => f.Message)), Severity.Error, o => o.RequireInteraction = true);
    }

    public void Cancel() => _cts?.Cancel();

    public void Notify(string message, Severity severity = Severity.Info) => _snackbar.Add(message, severity);

    private bool Begin(string? busyMessage, string? busyKey)
    {
        if (IsBusy)
        {
            _snackbar.Add("Another action is still running.", Severity.Warning);
            return false;
        }
        _cts = new CancellationTokenSource();
        IsBusy = true;
        BusyMessage = busyMessage;
        BusyKey = busyKey;
        ProgressText = null;
        ProgressFraction = null;
        Changed?.Invoke();
        return true;
    }

    private async Task End(bool refresh)
    {
        IsBusy = false;
        BusyMessage = null;
        BusyKey = null;
        ProgressText = null;
        ProgressFraction = null;
        _cts?.Dispose();
        _cts = null;
        Changed?.Invoke();
        if (refresh)
        {
            try { await _status.RefreshAsync(); }
            catch (Exception ex) { _logger.LogDebug(ex, "Refresh after action failed"); }
        }
    }

    private void OnProgress(PullProgress p)
    {
        var now = DateTime.UtcNow;
        var done = p.Status.StartsWith("Pull complete", StringComparison.OrdinalIgnoreCase) && p.LayerId is null;
        if (!done && now - _lastProgressPublish < TimeSpan.FromMilliseconds(250)) return;
        _lastProgressPublish = now;
        ProgressText = p.LayerId is null ? $"{p.ImageRef}: {p.Status}" : $"{p.ImageRef}: {p.Status} ({p.LayerId})";
        ProgressFraction = p.OverallFraction;
        Changed?.Invoke();
    }

    private void ShowError(Exception ex)
    {
        if (ex is not DwisDockerException) _logger.LogError(ex, "UI action failed");
        _snackbar.Add(Message(ex), Severity.Error, o => o.RequireInteraction = true);
    }

    private static string Message(Exception ex) => ex switch
    {
        DwisDockerException d => d.UserMessage,
        _ => ex.Message
    };

    public void Dispose() => _cts?.Cancel();
}
