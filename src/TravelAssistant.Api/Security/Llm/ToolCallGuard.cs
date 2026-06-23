namespace TravelAssistant.Api.Security.Llm;

/// <summary>
/// SEC-2 / C1 — Enforces per-agent tool allowlists. Any tool invocation
/// that is not on the agent's explicit allowlist throws
/// <see cref="ToolNotAllowedException"/>. The agent loop MUST surface
/// this exception, not swallow it.
/// </summary>
public sealed class ToolCallGuard
{
    private readonly IReadOnlyDictionary<string, HashSet<string>> _allowlist;

    public ToolCallGuard(IReadOnlyDictionary<string, IEnumerable<string>> allowlist)
    {
        ArgumentNullException.ThrowIfNull(allowlist);
        _allowlist = allowlist.ToDictionary(
            kv => kv.Key,
            kv => new HashSet<string>(kv.Value, StringComparer.Ordinal));
    }

    /// <summary>
    /// Static default allowlist for the three agents defined in SEC-2.
    /// Application-development-squad may extend by passing a config-driven
    /// dictionary into the constructor.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IEnumerable<string>> Defaults = new Dictionary<string, IEnumerable<string>>
    {
        ["ItineraryPlanner"] = new[] { "search_flights", "search_hotels", "get_destination_info" },
        ["BookingAgent"] = new[] { "book_flight", "book_hotel", "charge_card" },
        ["ResearchAgent"] = new[] { "web_search", "get_destination_info" },
    };

    public void Authorize(string agentName, string toolName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            throw new ToolNotAllowedException("(unknown)", toolName, "agent name was not supplied");
        }

        if (string.IsNullOrWhiteSpace(toolName))
        {
            throw new ToolNotAllowedException(agentName, "(none)", "tool name was not supplied");
        }

        if (!_allowlist.TryGetValue(agentName, out var tools))
        {
            throw new ToolNotAllowedException(agentName, toolName, "agent has no registered allowlist");
        }

        if (!tools.Contains(toolName))
        {
            throw new ToolNotAllowedException(agentName, toolName, "tool is not on the agent's allowlist");
        }
    }
}

public sealed class ToolNotAllowedException : Exception
{
    public string AgentName { get; }
    public string ToolName { get; }

    public ToolNotAllowedException(string agentName, string toolName, string reason)
        : base($"Tool '{toolName}' is not allowed for agent '{agentName}': {reason}.")
    {
        AgentName = agentName;
        ToolName = toolName;
    }
}
