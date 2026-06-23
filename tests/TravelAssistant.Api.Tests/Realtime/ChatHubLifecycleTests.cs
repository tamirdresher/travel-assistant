using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using TravelAssistant.Api.Realtime;
using Xunit;

namespace TravelAssistant.Api.Tests.Realtime;

// QA-4b — end-to-end lifecycle harness against the wired hub.
// Reproduces 3 defects on the APP-2 hub commit (orphan 1cdbbaf):
//
//   DEFECT-1 (wire): Program.cs has no AddSignalR()/MapHub<ChatHub>/DI for
//     ITurnRegistry+IGroundingTracker. All 3 SignalR-client tests below will
//     fail at hub.StartAsync() with 404 negotiate — the hub is unreachable
//     despite the commit message claim "Wired on /hubs/chat".
//
//   DEFECT-3 (race): TryCancel and SnapshotPending are not atomic. A streamer
//     racing the cancel can TrackPendingPatch after TryCancel returns but
//     before SnapshotPending reads. Those patches leak past the rollback emit.
//     DEFECT3_TryCancel_SnapshotPending_race_leaks_pending_patch reproduces.
//
//   DEFECT-2 (design): ChatHub.CoerceAndTrack is `internal` and binds to
//     Context.ConnectionId. Hub instances are transient per-call — a server
//     LLM streamer has no Hub with a valid Context. The grounding-coercion
//     gate is unreachable from the producer. Not exercised here (it's a
//     design defect, not a runtime fault) — fix needs an IGroundingGate
//     service that takes (connectionId, turnId) explicitly + an IHubContext
//     to broadcast.
//
// Add to tests/TravelAssistant.Api.Tests/Realtime/ on the branch that
// actually contains src/TravelAssistant.Api/Realtime/ChatHub.cs.
// Csproj needs: <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="9.0.0" />
public class ChatHubLifecycleTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ChatHubLifecycleTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private async Task<HubConnection> ConnectAsync()
    {
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "/hubs/chat"),
                opts =>
                {
                    opts.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    opts.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                })
            .Build();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await hub.StartAsync(cts.Token);
        return hub;
    }

    [Fact]
    public async Task G005_duplicate_StartTurn_returns_IsNew_false()
    {
        await using var hub = await ConnectAsync();
        var first = await hub.InvokeAsync<TurnAck>("StartTurn", "turn-G005", "plan a trip");
        var second = await hub.InvokeAsync<TurnAck>("StartTurn", "turn-G005", "plan a trip");
        Assert.True(first.IsNew, "first StartTurn must be new");
        Assert.False(second.IsNew, "second StartTurn with same id must be a replay");
    }

    [Fact]
    public async Task G004_CancelTurn_emits_turn_end_cancelled()
    {
        await using var hub = await ConnectAsync();
        var ends = new List<TurnEnd>();
        var endTcs = new TaskCompletionSource();
        hub.On<TurnEnd>(ChatHubMethods.TurnEnd, e => { ends.Add(e); endTcs.TrySetResult(); });

        await hub.InvokeAsync<TurnAck>("StartTurn", "turn-G004", "plan");
        var ack = await hub.InvokeAsync<CancelAck>("CancelTurn", "turn-G004");
        await Task.WhenAny(endTcs.Task, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.True(ack.Cancelled, "CancelTurn must report Cancelled=true for an active turn");
        Assert.Contains(ends, e => e.TurnId == "turn-G004" && e.Status == TurnStatus.Cancelled);
    }

    [Fact]
    public async Task CancelTurn_is_idempotent_second_call_returns_false()
    {
        await using var hub = await ConnectAsync();
        await hub.InvokeAsync<TurnAck>("StartTurn", "turn-cancel-idem", "x");
        var first = await hub.InvokeAsync<CancelAck>("CancelTurn", "turn-cancel-idem");
        var second = await hub.InvokeAsync<CancelAck>("CancelTurn", "turn-cancel-idem");
        Assert.True(first.Cancelled);
        Assert.False(second.Cancelled);
    }

    // DEFECT-3 reproduction (registry-only, no wire dependency).
    [Fact]
    public void DEFECT3_TryCancel_SnapshotPending_race_leaks_pending_patch()
    {
        var sut = new TurnRegistry();
        sut.Acquire("conn-1", "turn-race");
        sut.TrackPendingPatch("conn-1", "turn-race",
            new PatchOp("add", "/flights/0", new { id = "F1" }, PatchStatus.Pending));

        Assert.True(sut.TryCancel("conn-1", "turn-race"));

        // Streamer (token poll hasn't fired yet) adds another pending patch.
        // After cancel, registry MUST refuse new pending patches for this turn.
        sut.TrackPendingPatch("conn-1", "turn-race",
            new PatchOp("add", "/hotels/0", new { id = "H1" }, PatchStatus.Pending));

        var snap = sut.SnapshotPending("conn-1", "turn-race");

        // Today: snap.Count == 2 (post-cancel patch leaked into rollback set,
        // and escapes the rollback emit on the opposite race ordering).
        // Fix: TurnRegistry.TryCancel should atomically snapshot+freeze.
        Assert.Single(snap);
        Assert.Equal("/flights/0", snap[0].Path);
    }
}
