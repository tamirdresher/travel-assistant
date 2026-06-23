using Microsoft.AspNetCore.SignalR;

namespace TravelAssistant.Api.Realtime;

// APP-2 ChatHub — slim wire surface. Per QA-4b lifecycle contract:
//   StartTurn(turnId, userText) -> TurnAck { IsNew }
//   CancelTurn(turnId)          -> CancelAck { Cancelled }
//   client callback `TurnEnd` carries TurnEnd { TurnId, Status, Reason }
//
// Grounding coercion is intentionally NOT on the Hub — see IGroundingGate
// (DEFECT-2 fix). The Hub only owns lifecycle methods that legitimately
// have a live Context.ConnectionId from the client invocation.
public sealed class ChatHub : Hub
{
    private readonly ITurnRegistry _turns;

    public ChatHub(ITurnRegistry turns)
    {
        ArgumentNullException.ThrowIfNull(turns);
        _turns = turns;
    }

    // G-005 — idempotent StartTurn. Duplicate turnId on the same connection
    // returns IsNew=false; no new turn is created.
    public Task<TurnAck> StartTurn(string turnId, string userText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(turnId);
        var isNew = _turns.Acquire(Context.ConnectionId, turnId);
        return Task.FromResult(new TurnAck(turnId, isNew));
    }

    // G-004 — CancelTurn is atomic + idempotent. First call cancels and
    // drains pending patches; subsequent calls return Cancelled=false.
    public async Task<CancelAck> CancelTurn(string turnId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(turnId);
        var connectionId = Context.ConnectionId;
        var (cancelled, snapshot) = _turns.TryCancelAndDrain(connectionId, turnId);

        if (cancelled)
        {
            // Emit rollback for everything that was pending at cancel time.
            // Patches that arrived after the atomic flip were rejected at
            // TrackPendingPatch (DEFECT-3 guard) and therefore are not here.
            foreach (var patch in snapshot)
            {
                await Clients.Caller
                    .SendAsync(ChatHubMethods.PatchRollback, patch)
                    .ConfigureAwait(false);
            }

            await Clients.Caller
                .SendAsync(ChatHubMethods.TurnEnd, new TurnEnd(turnId, TurnStatus.Cancelled, "client_cancel"))
                .ConfigureAwait(false);
        }

        return new CancelAck(turnId, cancelled);
    }
}
