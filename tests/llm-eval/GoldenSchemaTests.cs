using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Xunit;

namespace TravelAssistant.LlmEval;

/// <summary>
/// Schema + grading-rubric tests. These run on EVERY nightly build and on PRs that
/// touch <c>tests/llm-eval/**</c> so a malformed golden never makes it to a live
/// grading run. Live agent replay + LLM-graded scoring runs only when
/// <c>LLM_EVAL_LIVE=1</c> is set (nightly workflow), behind <see cref="LiveAgentReplayTests"/>.
/// </summary>
public sealed class GoldenSchemaTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static TheoryData<string> AllGoldens()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "goldens");
        var data = new TheoryData<string>();
        foreach (var f in Directory.EnumerateFiles(dir, "*.json"))
        {
            data.Add(Path.GetFileName(f));
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(AllGoldens))]
    public void Golden_Parses_And_Has_Required_Fields(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "goldens", fileName);
        var json = File.ReadAllText(path);
        var g = JsonSerializer.Deserialize<Golden>(json, Json);

        g.Should().NotBeNull();
        g!.Id.Should().NotBeNullOrWhiteSpace($"{fileName} must declare an id");
        g.Id.Should().MatchRegex("^G-\\d{3}$", "ids are G-### so they sort and are easy to reference");
        g.Messages.Should().NotBeNullOrEmpty($"{fileName} must contain at least one message");
        g.Messages.Should().OnlyContain(m => m.Role == "user" || m.Role == "assistant" || m.Role == "system");
        g.ExpectedStatus.Should().BeOneOf("Grounded", "Pending", "Refused");
    }

    [Theory]
    [MemberData(nameof(AllGoldens))]
    public void Golden_MustInclude_And_MustNotInclude_Do_Not_Overlap(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "goldens", fileName);
        var g = JsonSerializer.Deserialize<Golden>(File.ReadAllText(path), Json)!;
        var overlap = g.MustInclude.Intersect(g.MustNotInclude, StringComparer.OrdinalIgnoreCase).ToArray();
        overlap.Should().BeEmpty(
            $"{fileName} has tokens listed in BOTH must_include and must_not_include: [{string.Join(", ", overlap)}] — the rubric would be unsatisfiable");
    }

    [Fact]
    public void Rubric_Threshold_Matches_Documented_Policy()
    {
        // Documented in README.md — if these constants change, update the README in the same PR.
        Rubric.PerConversationMinTotal.Should().Be(11);
        Rubric.PerAxisMin.Should().Be(3);
        Rubric.SuiteMeanMin.Should().Be(12.5);
        Rubric.RegressionThresholdPct.Should().Be(10);
    }
}

internal sealed record Golden(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("tags")] string[] Tags,
    [property: JsonPropertyName("messages")] GoldenMessage[] Messages,
    [property: JsonPropertyName("must_include")] string[] MustInclude,
    [property: JsonPropertyName("must_not_include")] string[] MustNotInclude,
    [property: JsonPropertyName("expected_status")] string ExpectedStatus);

internal sealed record GoldenMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

internal static class Rubric
{
    public const int PerConversationMinTotal = 11;
    public const int PerAxisMin = 3;
    public const double SuiteMeanMin = 12.5;
    public const int RegressionThresholdPct = 10;
}
