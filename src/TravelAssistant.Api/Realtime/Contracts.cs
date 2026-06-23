namespace TravelAssistant.Api.Realtime;

// APP-2 hub wire contracts. Locked by QA-4b lifecycle harness.
// Any rename/shape change here MUST be coordinated with quality-testing-squad
// because ChatHubLifecycleTests is the authoritative wire fixture.

public static class ChatHubMethods
{
    public const string TurnStart = nameof(TurnStart);
    public const string TurnEnd = nameof(TurnEnd);
    public const string PatchEmit = nameof(PatchEmit);
    public const string PatchRollback = nameof(PatchRollback);
}

public enum TurnStatus
{
    Pending = 0,
    Completed = 1,
    Cancelled = 2,
    Failed = 3,
}

public enum PatchStatus
{
    Pending = 0,
    Applied = 1,
    RolledBack = 2,
}

public sealed record TurnAck(string TurnId, bool IsNew);

public sealed record CancelAck(string TurnId, bool Cancelled);

public sealed record TurnEnd(string TurnId, TurnStatus Status, string? Reason = null);

public sealed record PatchOp(string Op, string Path, object? Value, PatchStatus Status);
