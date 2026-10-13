using System.Text.Json;
using System.Text.Json.Serialization;
using DWIS.Docker.Core.Models;
using Microsoft.Extensions.Logging;

namespace DWIS.Docker.Core.Services;

public interface ISettingsStore
{
    /// <summary>Current settings. Treat as read-only; call <see cref="AppSettings.Clone"/> to edit.</summary>
    AppSettings Current { get; }

    /// <summary>Path of the settings file, or null when settings are kept in memory.</summary>
    string? FilePath { get; }

    event EventHandler<AppSettings>? Changed;

    /// <summary>Validates, persists and publishes the new settings.</summary>
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);

    Task ReloadAsync(CancellationToken ct = default);
}

public abstract class SettingsStoreBase : ISettingsStore
{
    private readonly object _gate = new();
    private AppSettings _current = new();

    public AppSettings Current
    {
        get { lock (_gate) return _current; }
        protected set { lock (_gate) _current = value; }
    }

    public abstract string? FilePath { get; }

    public event EventHandler<AppSettings>? Changed;

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        var errors = settings.Validate();
        if (errors.Count > 0)
            throw new ConfigValidationException("Settings are not valid: " + string.Join(" ", errors), errors);

        await PersistAsync(settings, ct).ConfigureAwait(false);
        Current = settings.Clone();
        Changed?.Invoke(this, Current);
    }

    public abstract Task ReloadAsync(CancellationToken ct = default);

    protected abstract Task PersistAsync(AppSettings settings, CancellationToken ct);
}

/// <summary>Settings kept in memory only (used by the updater and tests).</summary>
public sealed class InMemorySettingsStore : SettingsStoreBase
{
    public InMemorySettingsStore(AppSettings? initial = null)
    {
        if (initial is not null) Current = initial;
    }

    public override string? FilePath => null;
    public override Task ReloadAsync(CancellationToken ct = default) => Task.CompletedTask;
    protected override Task PersistAsync(AppSettings settings, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Settings stored as <c>settings.json</c> in the data directory.</summary>
public sealed class JsonSettingsStore : SettingsStoreBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<JsonSettingsStore> _logger;

    public JsonSettingsStore(HostEnvironment env, ILogger<JsonSettingsStore> logger)
    {
        _logger = logger;
        FilePath = Path.Combine(env.DataDirectory, "settings.json");
        Load();
    }

    public override string? FilePath { get; }

    public override Task ReloadAsync(CancellationToken ct = default)
    {
        Load();
        return Task.CompletedTask;
    }

    protected override async Task PersistAsync(AppSettings settings, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(FilePath!)!;
        Directory.CreateDirectory(dir);
        var tmp = FilePath + ".tmp";
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(tmp, json, ct).ConfigureAwait(false);
        File.Move(tmp, FilePath!, overwrite: true);
    }

    private void Load()
    {
        if (!File.Exists(FilePath))
        {
            _logger.LogInformation("No settings file at {Path}; using defaults", FilePath);
            Current = new AppSettings();
            return;
        }

        try
        {
            var json = File.ReadAllText(FilePath!);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            if (loaded.Hubs.Count == 0) loaded.Hubs.Add(HubDefinition.Norce);
            var errors = loaded.Validate();
            if (errors.Count > 0)
                _logger.LogWarning("Settings file has problems: {Errors}", string.Join(" ", errors));
            Current = loaded;
        }
        catch (Exception ex)
        {
            var bad = FilePath + ".bad";
            _logger.LogWarning(ex, "Settings file {Path} is unreadable; renaming to {Bad} and using defaults", FilePath, bad);
            try { File.Move(FilePath!, bad, overwrite: true); } catch { /* best effort */ }
            Current = new AppSettings();
        }
    }
}
