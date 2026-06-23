using Microsoft.AspNetCore.SignalR;

namespace TravelAssistant.Api.Realtime;

// XD-locked event vocabulary contract — see docs/design/conversation-ux.md §1, §4.
// Server-emitted events flow as named SignalR client methods (see ChatHubMethods).
// Hard rules enforced server-side:
//   (1) per-turnId ordering (single in-flight ack chain per turn)
//   (2) turn.cancel rolls back patches still in Pending
//   (3) duplicate turn.start with the same turnId is idempotent
//   (4) ungrounded specifics (no tool.result for this turnId) are coerced to Pending
public sealed class ChatHub : Hub
{
    private readonly ITurnRegistry _turns;
    private readonly IGroundingTracker _grounding;

    public ChatHub(ITurnRegistry turns, IGroundingTracker grounding)
    {
        ArgumentNullException.ThrowIfNull(turns);
        ArgumentNullException.ThrowIfNull(grounding);
        _turns = turns;
        _grounding = grounding;
    }

    // Client → server. Begins a new turn (or no-ops if turnId was already seen).
    // Returns the lease so client knows whether the turn is fresh or being replayed.
    public TurnAck StartTurn(string turnId, string userText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(turnId);
        var lease = _turns.Acquire(Context.ConnectionId, turnId);
        return new TurnAck(turnId, lease.IsNew);
    }

    // Client → server. Cancels an in-flight turn; server stops streaming and
    // rolls back patches that were marked Pending.
    public CancelAck CancelTurn(string turnId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(turnId);
        var cancelled = _turns.TryCancel(Context.ConnectionId, turnId);
        if (cancelled)
        {
            // Roll back pending patches by emitting an inverse patch op set.
            var pending = _turns.SnapshotPending(Context.ConnectionId, turnId);
            if (pending.Count > 0)
            {
                var rollback = pending.Select(p => new PatchOp("remove", p.Path, null, PatchStatus.Grounded)).ToArray();
                _ = Clients.Caller.SendAsync(ChatHubMethods.ItineraryPatch, new ItineraryPatch(rollback));
            }
            _ = Clients.Caller.SendAsync(
                ChatHubMethods.TurnEnd,
                new TurnEnd(turnId, TurnStatus.Cancelled, new TurnUsage(0, 0, 0m)));
        }
        return new CancelAck(turnId, cancelled);
    }

    // Server-internal: invoked by the turn pipeline (LLM streamer) to push patches.
    // Enforces grounding coercion — if assistant emits Grounded patches without a
    // tool.result on this turnId, they're forced to Pending.
    internal ItineraryPatch CoerceAndTrack(string turnId, ItineraryPatch incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        var grounded = _grounding.HasGrounding(Context.ConnectionId, turnId);
        var coerced = incoming.Ops.Select(op =>
            (op.Status == PatchStatus.Grounded && !grounded)
                ? op with { Status = PatchStatus.Pending }
                : op).ToArray();
        foreach (var op in coerced)
            _turns.TrackPendingPatch(Context.ConnectionId, turnId, op);
        return new ItineraryPatch(coerced);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (_turns is TurnRegistry concrete)
            concrete.RemoveConnection(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

public sealed record TurnAck(string TurnId, bool IsNew);
public sealed record CancelAck(string TurnId, bool Cancelled);
