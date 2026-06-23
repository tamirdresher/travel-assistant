using System.Collections.Concurrent;

namespace TravelAssistant.Api.Realtime;

// Tracks active turns per connection so we can:
//   (1) order events per turnId via per-turn locks,
//   (2) honor turn.cancel mid-stream with rollback of pending patches,
//   (3) make duplicate turn.start idempotent (retry-safe).
//
// In-memory single-node impl. Multi-replica deployments need a backplane
// (Redis pub/sub via SignalR) — APP-1 contract.
public interface ITurnRegistry
{
    TurnLease Acquire(string connectionId, string turnId);
    bool TryCancel(string connectionId, string turnId);
    void TrackPendingPatch(string connectionId, string turnId, PatchOp op);
    IReadOnlyList<PatchOp> SnapshotPending(string connectionId, string turnId);
    void Complete(string connectionId, string turnId);
}

public sealed record TurnLease(string TurnId, bool IsNew, CancellationToken CancellationToken);

internal sealed class TurnRegistry : ITurnRegistry
{
    private sealed class TurnState
    {
        public CancellationTokenSource Cts { get; } = new();
        public List<PatchOp> Pending { get; } = new();
        public bool Completed;
        public int AcquireCount;
    }

    private readonly ConcurrentDictionary<(string ConnId, string TurnId), TurnState> _turns = new();

    public TurnLease Acquire(string connectionId, string turnId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(turnId);

        var state = _turns.GetOrAdd((connectionId, turnId), _ => new TurnState());
        var ordinal = Interlocked.Increment(ref state.AcquireCount);
        return new TurnLease(turnId, ordinal == 1, state.Cts.Token);
    }

    public bool TryCancel(string connectionId, string turnId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(turnId);
        if (!_turns.TryGetValue((connectionId, turnId), out var state)) return false;
        if (state.Completed) return false;
        try { state.Cts.Cancel(); } catch (ObjectDisposedException) { return false; }
        return true;
    }

    public void TrackPendingPatch(string connectionId, string turnId, PatchOp op)
    {
        ArgumentNullException.ThrowIfNull(op);
        if (!_turns.TryGetValue((connectionId, turnId), out var state)) return;
        if (op.Status != PatchStatus.Pending) return;
        lock (state.Pending) { state.Pending.Add(op); }
    }

    public IReadOnlyList<PatchOp> SnapshotPending(string connectionId, string turnId)
    {
        if (!_turns.TryGetValue((connectionId, turnId), out var state))
            return Array.Empty<PatchOp>();
        lock (state.Pending) { return state.Pending.ToArray(); }
    }

    public void Complete(string connectionId, string turnId)
    {
        if (!_turns.TryGetValue((connectionId, turnId), out var state)) return;
        state.Completed = true;
        try { state.Cts.Dispose(); } catch { /* idempotent */ }
    }

    internal void RemoveConnection(string connectionId)
    {
        foreach (var key in _turns.Keys.Where(k => k.ConnId == connectionId).ToArray())
        {
            if (_turns.TryRemove(key, out var state))
            {
                try { if (!state.Cts.IsCancellationRequested) state.Cts.Cancel(); } catch { }
                try { state.Cts.Dispose(); } catch { }
            }
        }
    }
}
