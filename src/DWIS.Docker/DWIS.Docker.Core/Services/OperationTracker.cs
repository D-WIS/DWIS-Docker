using DWIS.Docker.Core.Models;

namespace DWIS.Docker.Core.Services;

/// <summary>Tracks long-running operations so every view can show the same spinner for the same row.</summary>
public sealed class OperationTracker
{
    private readonly Dictionary<Guid, ActiveOperation> _ops = new();
    private readonly object _gate = new();

    public event Action? Changed;

    public IReadOnlyList<ActiveOperation> Current
    {
        get { lock (_gate) return _ops.Values.ToList(); }
    }

    public IDisposable Begin(string key, string description)
    {
        var id = Guid.NewGuid();
        lock (_gate) _ops[id] = new ActiveOperation(key, description, null);
        Changed?.Invoke();
        return new Handle(this, id);
    }

    public void Update(IDisposable handle, double? fraction, string? description = null)
    {
        if (handle is not Handle h) return;
        lock (_gate)
        {
            if (!_ops.TryGetValue(h.Id, out var op)) return;
            _ops[h.Id] = op with { Fraction = fraction, Description = description ?? op.Description };
        }
        Changed?.Invoke();
    }

    private void End(Guid id)
    {
        lock (_gate) _ops.Remove(id);
        Changed?.Invoke();
    }

    private sealed class Handle(OperationTracker owner, Guid id) : IDisposable
    {
        public Guid Id { get; } = id;
        public void Dispose() => owner.End(Id);
    }
}
