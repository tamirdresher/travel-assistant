using System.Collections.Concurrent;

namespace TravelAssistant.Api.Realtime;

// Tracks whether a turn has produced any tool.result (i.e. has grounding).
// Used by ChatHub to coerce ungrounded specifics to Pending status, per
// XD hard rule: "if assistant emits user-facing specifics (price/time/address)
// without a tool.result citation in the same turn, client coerces to pending".
// Server-side enforcement so the client doesn't have to police it.
public interface IGroundingTracker
{
    void RecordToolResult(string connectionId, string turnId);
    bool HasGrounding(string connectionId, string turnId);
}

internal sealed class GroundingTracker : IGroundingTracker
{
    private readonly ConcurrentDictionary<(string, string), bool> _grounded = new();

    public void RecordToolResult(string connectionId, string turnId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(turnId);
        _grounded[(connectionId, turnId)] = true;
    }

    public bool HasGrounding(string connectionId, string turnId)
    {
        if (string.IsNullOrWhiteSpace(connectionId) || string.IsNullOrWhiteSpace(turnId))
            return false;
        return _grounded.TryGetValue((connectionId, turnId), out var v) && v;
    }
}
