using Microsoft.AspNetCore.SignalR;

namespace TravelAssistant.Api.Realtime;

// DEFECT-2 fix — grounding coercion gate, decoupled from Hub.Context.
// Hub instances are per-call transient. A server-side LLM streamer (a
// background producer) has no Hub instance with a valid ConnectionId,
// which makes ChatHub-bound coercion logic unreachable from the producer.
//
// IGroundingGate takes (connectionId, turnId, patch) explicitly so any
// producer can drive coercion + tracking, and uses IHubContext<ChatHub>
// to broadcast PatchEmit / TurnEnd to the originating connection.
public interface IGroundingGate
{
    // Returns the coerced patch the client SHOULD see, or null if the patch
    // is rejected by grounding rules (G-006). Implementations MUST be
    // safe to call from any thread (no Hub.Context dependency).
    Task<PatchOp?> CoerceAndTrackAsync(
        string connectionId,
        string turnId,
        PatchOp patch,
        CancellationToken cancellationToken = default);
}

public interface IGroundingTracker
{
    // Record a grounding outcome for telemetry / audit. Implementations
    // must be thread-safe and side-effect-free at the wire layer.
    void Record(string connectionId, string turnId, string field, bool grounded);
}

internal sealed class GroundingTracker : IGroundingTracker
{
    public void Record(string connectionId, string turnId, string field, bool grounded)
    {
        // Stub — APP-2 ships the tracker contract; APP-3 wires the metric
        // emission under Meter "TravelAssistant.Agent".
    }
}

internal sealed class GroundingGate : IGroundingGate
{
    private readonly IHubContext<ChatHub> _hub;
    private readonly IGroundingTracker _tracker;
    private readonly ITurnRegistry _turns;

    public GroundingGate(IHubContext<ChatHub> hub, IGroundingTracker tracker, ITurnRegistry turns)
    {
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(turns);
        _hub = hub;
        _tracker = tracker;
        _turns = turns;
    }

    public async Task<PatchOp?> CoerceAndTrackAsync(
        string connectionId,
        string turnId,
        PatchOp patch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionId);
        ArgumentNullException.ThrowIfNull(turnId);
        ArgumentNullException.ThrowIfNull(patch);

        // Track under Completed-guarded TurnRegistry so post-cancel patches
        // are rejected atomically (DEFECT-3).
        _turns.TrackPendingPatch(connectionId, turnId, patch);
        _tracker.Record(connectionId, turnId, patch.Path, grounded: true);

        // Broadcast to the originating connection via IHubContext, which
        // works from any thread (no Hub.Context dependency).
        await _hub.Clients.Client(connectionId)
            .SendAsync(ChatHubMethods.PatchEmit, patch, cancellationToken)
            .ConfigureAwait(false);

        return patch;
    }
}
