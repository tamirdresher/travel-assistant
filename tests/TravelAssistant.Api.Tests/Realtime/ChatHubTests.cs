using TravelAssistant.Api.Realtime;
using Xunit;

namespace TravelAssistant.Api.Tests.Realtime;

// APP-2 acceptance tests for the three XD-locked hard rules:
//   (1) events ordered per turnId (single in-flight chain per turn)
//   (2) cancel mid-stream rolls back pending patches
//   (3) duplicate turn.start with same turnId is idempotent
// Plus the grounding-coercion hard rule (ungrounded specifics -> Pending).
public class TurnRegistryTests
{
    [Fact]
    public void Acquire_first_call_returns_IsNew_true()
    {
        var sut = new TurnRegistry();
        var lease = sut.Acquire("conn-1", "turn-A");
        Assert.True(lease.IsNew);
        Assert.Equal("turn-A", lease.TurnId);
    }

    [Fact]
    public void Acquire_duplicate_turnId_returns_IsNew_false()
    {
        var sut = new TurnRegistry();
        sut.Acquire("conn-1", "turn-A");
        var second = sut.Acquire("conn-1", "turn-A");
        Assert.False(second.IsNew);
    }

    [Fact]
    public void Acquire_same_turnId_different_connection_is_independent()
    {
        var sut = new TurnRegistry();
        var a = sut.Acquire("conn-1", "turn-A");
        var b = sut.Acquire("conn-2", "turn-A");
        Assert.True(a.IsNew);
        Assert.True(b.IsNew);
    }

    [Fact]
    public void TryCancel_signals_token_and_returns_true()
    {
        var sut = new TurnRegistry();
        var lease = sut.Acquire("conn-1", "turn-A");
        Assert.False(lease.CancellationToken.IsCancellationRequested);
        Assert.True(sut.TryCancel("conn-1", "turn-A"));
        Assert.True(lease.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public void TryCancel_unknown_turn_returns_false()
    {
        var sut = new TurnRegistry();
        Assert.False(sut.TryCancel("conn-1", "ghost"));
    }

    [Fact]
    public void SnapshotPending_returns_only_pending_ops_in_order()
    {
        var sut = new TurnRegistry();
        sut.Acquire("conn-1", "turn-A");
        sut.TrackPendingPatch("conn-1", "turn-A",
            new PatchOp("add", "/flights/0", new { id = "F1" }, PatchStatus.Pending));
        sut.TrackPendingPatch("conn-1", "turn-A",
            new PatchOp("add", "/hotels/0", new { id = "H1" }, PatchStatus.Grounded)); // ignored
        sut.TrackPendingPatch("conn-1", "turn-A",
            new PatchOp("add", "/flights/1", new { id = "F2" }, PatchStatus.Pending));

        var snap = sut.SnapshotPending("conn-1", "turn-A");
        Assert.Equal(2, snap.Count);
        Assert.Equal("/flights/0", snap[0].Path);
        Assert.Equal("/flights/1", snap[1].Path);
    }

    [Fact]
    public void Complete_then_TryCancel_returns_false()
    {
        var sut = new TurnRegistry();
        sut.Acquire("conn-1", "turn-A");
        sut.Complete("conn-1", "turn-A");
        Assert.False(sut.TryCancel("conn-1", "turn-A"));
    }
}

public class GroundingTrackerTests
{
    [Fact]
    public void HasGrounding_false_by_default()
    {
        var sut = new GroundingTracker();
        Assert.False(sut.HasGrounding("c", "t"));
    }

    [Fact]
    public void RecordToolResult_then_HasGrounding_true()
    {
        var sut = new GroundingTracker();
        sut.RecordToolResult("c", "t");
        Assert.True(sut.HasGrounding("c", "t"));
    }

    [Fact]
    public void Grounding_is_scoped_per_turn()
    {
        var sut = new GroundingTracker();
        sut.RecordToolResult("c", "t1");
        Assert.True(sut.HasGrounding("c", "t1"));
        Assert.False(sut.HasGrounding("c", "t2"));
    }
}

public class ChatEventContractTests
{
    [Theory]
    [InlineData("turn.start", nameof(ChatHubMethods.TurnStart))]
    [InlineData("token", nameof(ChatHubMethods.Token))]
    [InlineData("tool.call", nameof(ChatHubMethods.ToolCall))]
    [InlineData("tool.result", nameof(ChatHubMethods.ToolResult))]
    [InlineData("itinerary.patch", nameof(ChatHubMethods.ItineraryPatch))]
    [InlineData("turn.end", nameof(ChatHubMethods.TurnEnd))]
    [InlineData("turn.error", nameof(ChatHubMethods.TurnError))]
    public void Hub_method_names_match_XD_contract(string expected, string constantName)
    {
        var actual = typeof(ChatHubMethods).GetField(constantName)!.GetRawConstantValue();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PatchStatus_values_lock_to_XD_vocab()
    {
        Assert.Equal(0, (int)PatchStatus.Pending);
        Assert.Equal(1, (int)PatchStatus.Grounded);
    }

    [Fact]
    public void TurnStatus_includes_success_cancelled_error()
    {
        var names = Enum.GetNames<TurnStatus>();
        Assert.Contains("Success", names);
        Assert.Contains("Cancelled", names);
        Assert.Contains("Error", names);
    }
}
