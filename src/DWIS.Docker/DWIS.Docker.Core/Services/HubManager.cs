using DWIS.Docker.Core.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DWIS.Docker.Core.Services;

/// <summary>Manages the SignalR connections to the blackboard hubs listed in the settings.</summary>
public interface IHubService
{
    IReadOnlyList<HubState> Hubs { get; }

    event EventHandler? Changed;

    HubState? Find(string hubId);

    Task ConnectAsync(string hubId, CancellationToken ct = default);
    Task DisconnectAsync(string hubId, CancellationToken ct = default);
    Task RefreshGroupsAsync(string hubId, CancellationToken ct = default);

    /// <summary>With a password the group is created on the hub; without, it is only remembered locally until a blackboard joins it.</summary>
    Task CreateGroupAsync(string hubId, string group, string? password, CancellationToken ct = default);

    Task SetGroupPasswordAsync(string hubId, string group, string password, CancellationToken ct = default);
    Task<bool> ClearGroupManifestsAsync(string hubId, string group, CancellationToken ct = default);
}

/// <summary>Used when hubs are disabled (the updater).</summary>
public sealed class NullHubService : IHubService
{
    public IReadOnlyList<HubState> Hubs => [];
    public event EventHandler? Changed { add { } remove { } }
    public HubState? Find(string hubId) => null;
    public Task ConnectAsync(string hubId, CancellationToken ct = default) => throw Off();
    public Task DisconnectAsync(string hubId, CancellationToken ct = default) => throw Off();
    public Task RefreshGroupsAsync(string hubId, CancellationToken ct = default) => throw Off();
    public Task CreateGroupAsync(string hubId, string group, string? password, CancellationToken ct = default) => throw Off();
    public Task SetGroupPasswordAsync(string hubId, string group, string password, CancellationToken ct = default) => throw Off();
    public Task<bool> ClearGroupManifestsAsync(string hubId, string group, CancellationToken ct = default) => throw Off();
    private static HubUnavailableException Off() => new("Hub support is disabled.");
}

public sealed class HubManager : IHubService, IHostedService, IAsyncDisposable
{
    private readonly ISettingsStore _settings;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<HubManager> _logger;
    private readonly Dictionary<string, HubClient> _clients = new();
    private readonly object _gate = new();
    private CancellationTokenSource? _loopCts;
    private Task? _loop;

    /// <summary>How long to wait before trying again to connect a hub that failed (a local hub may still be starting).</summary>
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(15);

    public HubManager(ISettingsStore settings, ILoggerFactory loggerFactory)
    {
        _settings = settings;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<HubManager>();
        _settings.Changed += (_, s) => _ = ReconcileAsync(s, CancellationToken.None);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<HubState> Hubs
    {
        get
        {
            lock (_gate)
                return _settings.Current.Hubs
                    .Select(h => _clients.TryGetValue(h.Id, out var c) ? c.ToState() : new HubState(h, HubConnectionStatus.Disconnected, null, [], null))
                    .ToList();
        }
    }

    public HubState? Find(string hubId) => Hubs.FirstOrDefault(h => h.Hub.Id == hubId);

    // ---------------------------------------------------------------- hosted service

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await ReconcileAsync(_settings.Current, cancellationToken).ConfigureAwait(false);
        _loopCts = new CancellationTokenSource();
        _loop = Task.Run(() => PollLoopAsync(_loopCts.Token), CancellationToken.None);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _loopCts?.Cancel();
        List<HubClient> clients;
        lock (_gate) clients = _clients.Values.ToList();
        foreach (var c in clients)
            await c.DisposeAsync().ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => new(StopAsync(CancellationToken.None));

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_settings.Current.PollingInterval, ct).ConfigureAwait(false);
                List<HubClient> clients;
                lock (_gate) clients = _clients.Values.ToList();
                foreach (var c in clients)
                {
                    if (c.Status == HubConnectionStatus.Connected)
                        await c.RefreshGroupsAsync(ct).ConfigureAwait(false);
                    else if (c.ShouldRetryConnect(RetryInterval))
                        _ = c.ConnectAsync(ct).ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Hub group poll failed");
            }
        }
    }

    /// <summary>Creates, replaces and removes clients so they match the settings.</summary>
    private async Task ReconcileAsync(AppSettings settings, CancellationToken ct)
    {
        var toDispose = new List<HubClient>();
        var toConnect = new List<HubClient>();
        lock (_gate)
        {
            var wanted = settings.Hubs.ToDictionary(h => h.Id);
            foreach (var (id, client) in _clients.ToList())
            {
                if (!wanted.TryGetValue(id, out var def) || def.BaseUrl != client.Definition.BaseUrl)
                {
                    _clients.Remove(id);
                    toDispose.Add(client);
                }
                else if (def != client.Definition)
                {
                    client.Definition = def;
                }
            }
            foreach (var def in settings.Hubs)
            {
                if (_clients.ContainsKey(def.Id)) continue;
                var client = new HubClient(def, _loggerFactory.CreateLogger<HubClient>());
                client.Changed += () => Changed?.Invoke(this, EventArgs.Empty);
                _clients[def.Id] = client;
                if (def.AutoConnect) toConnect.Add(client);
            }
        }

        foreach (var c in toDispose)
            await c.DisposeAsync().ConfigureAwait(false);
        foreach (var c in toConnect)
            _ = c.ConnectAsync(ct);

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private HubClient Get(string hubId)
    {
        lock (_gate)
        {
            if (_clients.TryGetValue(hubId, out var c)) return c;
        }
        throw new HubUnavailableException("Unknown hub.");
    }

    // ---------------------------------------------------------------- operations

    public Task ConnectAsync(string hubId, CancellationToken ct = default) => Get(hubId).ConnectAsync(ct);
    public Task DisconnectAsync(string hubId, CancellationToken ct = default) => Get(hubId).DisconnectAsync(ct);
    public Task RefreshGroupsAsync(string hubId, CancellationToken ct = default) => Get(hubId).RefreshGroupsAsync(ct);
    public Task CreateGroupAsync(string hubId, string group, string? password, CancellationToken ct = default) => Get(hubId).CreateGroupAsync(group, password, ct);
    public Task SetGroupPasswordAsync(string hubId, string group, string password, CancellationToken ct = default) => Get(hubId).SetGroupPasswordAsync(group, password, ct);
    public Task<bool> ClearGroupManifestsAsync(string hubId, string group, CancellationToken ct = default) => Get(hubId).ClearGroupManifestsAsync(group, ct);
}

/// <summary>One SignalR connection to one hub.</summary>
internal sealed class HubClient : IAsyncDisposable
{
    private readonly ILogger<HubClient> _logger;
    private readonly HubConnection _connection;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly Dictionary<string, HubGroupInfo> _groups = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private HubConnectionStatus _status = HubConnectionStatus.Disconnected;
    private string? _error;
    private DateTimeOffset? _groupsUpdatedAt;
    private bool _userDisconnected;
    private DateTimeOffset _lastConnectAttempt = DateTimeOffset.MinValue;

    /// <summary>True when this auto-connect hub is not connected, the user did not disconnect it, and the last attempt is old enough.</summary>
    public bool ShouldRetryConnect(TimeSpan retryInterval)
    {
        if (!Definition.AutoConnect || _userDisconnected) return false;
        if (Status is HubConnectionStatus.Connected or HubConnectionStatus.Connecting or HubConnectionStatus.Reconnecting) return false;
        return DateTimeOffset.UtcNow - _lastConnectAttempt >= retryInterval;
    }

    public HubClient(HubDefinition definition, ILogger<HubClient> logger)
    {
        Definition = definition;
        _logger = logger;
        _connection = new HubConnectionBuilder()
            .WithUrl(definition.Endpoint)
            .WithAutomaticReconnect()
            .AddMessagePackProtocol()
            .Build();
        _connection.Reconnecting += ex => { Set(HubConnectionStatus.Reconnecting, ex?.Message); return Task.CompletedTask; };
        _connection.Reconnected += connectionId => { Set(HubConnectionStatus.Connected, null); _ = RefreshGroupsAsync(CancellationToken.None); return Task.CompletedTask; };
        _connection.Closed += ex =>
        {
            Set(_userDisconnected || ex is null ? HubConnectionStatus.Disconnected : HubConnectionStatus.Failed, ex?.Message);
            return Task.CompletedTask;
        };
    }

    public HubDefinition Definition { get; set; }
    public HubConnectionStatus Status { get { lock (_gate) return _status; } }

    public event Action? Changed;

    public HubState ToState()
    {
        lock (_gate)
            return new HubState(Definition, _status, _error, _groups.Values.OrderBy(g => g.Name).ToList(), _groupsUpdatedAt);
    }

    private void Set(HubConnectionStatus status, string? error)
    {
        lock (_gate)
        {
            _status = status;
            _error = error;
        }
        Changed?.Invoke();
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        await _connectLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_connection.State == HubConnectionState.Connected) return;
            _userDisconnected = false;
            _lastConnectAttempt = DateTimeOffset.UtcNow;
            Set(HubConnectionStatus.Connecting, null);
            await _connection.StartAsync(ct).ConfigureAwait(false);
            Set(HubConnectionStatus.Connected, null);
            _logger.LogInformation("Connected to hub {Name} ({Url})", Definition.Name, Definition.Endpoint);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("Could not connect to hub {Name}: {Message}", Definition.Name, ex.Message);
            Set(HubConnectionStatus.Failed, ex.Message);
            throw new HubUnavailableException($"Could not connect to {Definition.Name}: {ex.Message}", ex);
        }
        finally
        {
            _connectLock.Release();
        }
        await RefreshGroupsAsync(ct).ConfigureAwait(false);
    }

    public async Task DisconnectAsync(CancellationToken ct)
    {
        _userDisconnected = true;
        await _connection.StopAsync(ct).ConfigureAwait(false);
        Set(HubConnectionStatus.Disconnected, null);
    }

    public async Task RefreshGroupsAsync(CancellationToken ct)
    {
        if (_connection.State != HubConnectionState.Connected) return;
        try
        {
            var dto = await _connection.InvokeAsync<GroupDataDTO>("GetGroupData", ct).ConfigureAwait(false);
            if (dto?.GroupDatas is null)
            {
                Set(HubConnectionStatus.Connected, "The hub answered in an unexpected format (incompatible version?).");
                return;
            }
            lock (_gate)
            {
                var seen = new HashSet<string>();
                foreach (var g in dto.GroupDatas)
                {
                    if (string.IsNullOrEmpty(g.GroupName)) continue;
                    seen.Add(g.GroupName);
                    _groups[g.GroupName] = new HubGroupInfo(g.GroupName, g.GroupCount, false);
                }
                foreach (var stale in _groups.Where(kv => !kv.Value.IsLocalOnly && !seen.Contains(kv.Key)).Select(kv => kv.Key).ToList())
                    _groups.Remove(stale);
                _groupsUpdatedAt = DateTimeOffset.UtcNow;
                _error = null;
            }
            Changed?.Invoke();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "GetGroupData failed on {Name}", Definition.Name);
            Set(_status, ex.Message);
        }
    }

    public async Task CreateGroupAsync(string group, string? password, CancellationToken ct)
    {
        group = group.Trim();
        if (group.Length == 0) throw new DwisDockerException("Group name must not be empty.");
        if (!string.IsNullOrEmpty(password))
        {
            await SetGroupPasswordAsync(group, password, ct).ConfigureAwait(false);
        }
        lock (_gate)
        {
            if (!_groups.ContainsKey(group))
                _groups[group] = new HubGroupInfo(group, 0, IsLocalOnly: true);
        }
        Changed?.Invoke();
        await RefreshGroupsAsync(ct).ConfigureAwait(false);
    }

    public async Task SetGroupPasswordAsync(string group, string password, CancellationToken ct)
    {
        RequireConnected();
        try
        {
            await _connection.SendAsync("CreateProtectedGroup", group, password, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new HubUnavailableException($"Could not set the password of group '{group}': {ex.Message}", ex);
        }
    }

    public async Task<bool> ClearGroupManifestsAsync(string group, CancellationToken ct)
    {
        RequireConnected();
        try
        {
            return await _connection.InvokeAsync<bool>("ClearGroupManifests", group, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new HubUnavailableException($"Could not clear the manifests of group '{group}': {ex.Message}", ex);
        }
    }

    private void RequireConnected()
    {
        if (_connection.State != HubConnectionState.Connected)
            throw new HubUnavailableException($"Hub {Definition.Name} is not connected.");
    }

    public async ValueTask DisposeAsync()
    {
        _userDisconnected = true;
        try { await _connection.DisposeAsync().ConfigureAwait(false); }
        catch (Exception ex) { _logger.LogDebug(ex, "Hub dispose failed"); }
    }
}

// Server DTOs. Must be public with public setters and property names matching the hub exactly
// (MessagePack contractless resolver generates formatters only for public types).
public sealed class GroupDataDTO
{
    public GroupData[]? GroupDatas { get; set; }
}

public sealed class GroupData
{
    public string? GroupName { get; set; }
    public int GroupCount { get; set; }
}
