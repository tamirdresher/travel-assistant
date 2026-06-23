namespace TravelAssistant.Api.Realtime;

// XD-locked event vocabulary (see docs/design/conversation-ux.md §1, §4).
// All events ordered per turnId. Wire format is JSON; client method names
// are kebab-case-friendly via the constants below.

public static class ChatHubMethods
{
    public const string TurnStart = "turn.start";
    public const string Token = "token";
    public const string ToolCall = "tool.call";
    public const string ToolResult = "tool.result";
    public const string ItineraryPatch = "itinerary.patch";
    public const string TurnEnd = "turn.end";
    public const string TurnError = "turn.error";
}

public sealed record TurnStart(string TurnId, string UserText);
public sealed record TokenDelta(string TurnId, string Delta);
public sealed record ToolCall(string TurnId, string ToolId, string Name, string ArgsPreview);
public sealed record ToolResult(string ToolId, string Summary, IReadOnlyList<Citation> Citations);
public sealed record Citation(string Url, string Title);

public sealed record ItineraryPatch(IReadOnlyList<PatchOp> Ops);
public sealed record PatchOp(string Op, string Path, object? Value, PatchStatus Status);
public enum PatchStatus { Pending, Grounded }

public sealed record TurnEnd(string TurnId, TurnStatus Status, TurnUsage Usage);
public enum TurnStatus { Success, Cancelled, Error }
public sealed record TurnUsage(int TokensIn, int TokensOut, decimal CostUsd);

public sealed record TurnError(string TurnId, string Code, bool Retriable);
