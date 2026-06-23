using System.Collections.Concurrent;

namespace TravelAssistant.Api.Realtime;

public interface ITurnRegistry
{
    bool Acquire(string connectionId, string turnId);
    void TrackPendingPatch(string connectionId, string turnId, PatchOp patch);
    bool TryCancel(string connectionId, string turnId);
    IReadOnlyList<PatchOp> SnapshotPending(string connectionId, string turnId);
    // DEFECT-3 fix — atomic cancel+drain. Returns (cancelled, snapshot) in
    // one operation so a streamer racing on TrackPendingPatch cannot leak
    // post-cancel patches into the rollback set.
    (bool Cancelled, IReadOnlyList<PatchOp> Snapshot) TryCancelAndDrain(string connectionId, string turnId);
    bool Complete(string connectionId, string turnId, TurnStatus status);
    void Release(string connectionId, string turnId);
}

internal sealed class TurnState
{
    public bool Completed;
    public TurnStatus FinalStatus = TurnStatus.Pending;
    public readonly List<PatchOp> Pending = new();
    public readonly object Sync = new();
}

public sealed class TurnRegistry : ITurnRegistry
{
    private readonly ConcurrentDictionary<string, TurnState> _turns = new();

    private static string Key(string connectionId, string turnId)
    {
        ArgumentNullException.ThrowIfNull(connectionId);
        ArgumentNullException.ThrowIfNull(turnId);
        return connectionId + "\u0001" + turnId;
    }

    // G-005: duplicate StartTurn returns IsNew=false. Acquire is the
    // idempotency point — false = replay, true = new turn.
    public bool Acquire(string connectionId, string turnId)
    {
        var key = Key(connectionId, turnId);
        return _turns.TryAdd(key, new TurnState());
    }

    public void TrackPendingPatch(string connectionId, string turnId, PatchOp patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        if (!_turns.TryGetValue(Key(connectionId, turnId), out var state))
        {
            return;
        }
        lock (state.Sync)
        {
            // DEFECT-3 fix — Completed guard. Post-cancel patches MUST NOT
            // enter the pending set, even on a race ordering where the
            // streamer's token poll hasn't fired yet.
            if (state.Completed)
            {
                return;
            }
            state.Pending.Add(patch);
        }
    }

    public bool TryCancel(string connectionId, string turnId)
    {
        if (!_turns.TryGetValue(Key(connectionId, turnId), out var state))
        {
            return false;
        }
        lock (state.Sync)
        {
            if (state.Completed)
            {
                return false;
            }
            state.Completed = true;
            state.FinalStatus = TurnStatus.Cancelled;
            return true;
        }
    }

    public (bool Cancelled, IReadOnlyList<PatchOp> Snapshot) TryCancelAndDrain(string connectionId, string turnId)
    {
        if (!_turns.TryGetValue(Key(connectionId, turnId), out var state))
        {
            return (false, Array.Empty<PatchOp>());
        }
        lock (state.Sync)
        {
            if (state.Completed)
            {
                return (false, Array.Empty<PatchOp>());
            }
            state.Completed = true;
            state.FinalStatus = TurnStatus.Cancelled;
            var snapshot = state.Pending.ToArray();
            return (true, snapshot);
        }
    }

    public IReadOnlyList<PatchOp> SnapshotPending(string connectionId, string turnId)
    {
        if (!_turns.TryGetValue(Key(connectionId, turnId), out var state))
        {
            return Array.Empty<PatchOp>();
        }
        lock (state.Sync)
        {
            return state.Pending.ToArray();
        }
    }

    public bool Complete(string connectionId, string turnId, TurnStatus status)
    {
        if (!_turns.TryGetValue(Key(connectionId, turnId), out var state))
        {
            return false;
        }
        lock (state.Sync)
        {
            if (state.Completed)
            {
                return false;
            }
            state.Completed = true;
            state.FinalStatus = status;
            return true;
        }
    }

    public void Release(string connectionId, string turnId)
    {
        _turns.TryRemove(Key(connectionId, turnId), out _);
    }
}
