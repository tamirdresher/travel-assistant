using TravelAssistant.Security.Pii;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TravelAssistant.Security.Tests.Pii;

/// <summary>
/// SEC-1b — 20 golden test cases for <see cref="PiiRedactor"/>.
/// 16 adversarial (must be redacted), 4 benign (must NOT be redacted).
/// If a case fails, fix the redactor; do not relax the golden without filing
/// a decision in <c>.squad/decisions/inbox</c> first.
/// </summary>
public sealed class PiiRedactorGoldenTests
{
    public sealed class GoldenCase
    {
        public string Id { get; set; } = "";
        public string Category { get; set; } = "";
        public string Input { get; set; } = "";

        [YamlMember(Alias = "expected_redacted")]
        public string ExpectedRedacted { get; set; } = "";

        [YamlMember(Alias = "expected_matches")]
        public int ExpectedMatches { get; set; }

        public string? Notes { get; set; }
    }

    private sealed class GoldenDoc
    {
        public List<GoldenCase> Cases { get; set; } = new();
    }

    public static IEnumerable<object[]> AllCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Pii", "goldens.yaml");
        var yaml = File.ReadAllText(path);
        var doc = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build()
            .Deserialize<GoldenDoc>(yaml);

        foreach (var c in doc.Cases)
        {
            yield return new object[] { c };
        }
    }

    [Theory]
    [MemberData(nameof(AllCases))]
    public void Golden(GoldenCase c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var result = PiiRedactor.Redact(c.Input);

        Assert.Equal(c.ExpectedRedacted, result.Redacted);
        Assert.Equal(c.ExpectedMatches, result.Matches.Count);
    }

    [Fact]
    public void Corpus_HasAtLeast20Cases()
    {
        var cases = AllCases().ToList();
        Assert.True(cases.Count >= 20, $"Expected >=20 goldens, found {cases.Count}.");
    }

    [Fact]
    public void Corpus_HasAtLeastFourBenign()
    {
        var benign = AllCases()
            .Select(o => (GoldenCase)o[0])
            .Count(c => string.Equals(c.Category, "benign", StringComparison.OrdinalIgnoreCase));
        Assert.True(benign >= 4, $"Expected >=4 benign cases, found {benign}.");
    }

    [Fact]
    public void Luhn_RejectsKnownInvalid()
    {
        Assert.False(PiiRedactor.IsValidLuhn("1234567890123456"));
    }

    [Fact]
    public void Luhn_AcceptsKnownValid()
    {
        Assert.True(PiiRedactor.IsValidLuhn("4532015112830366"));
    }

    [Fact]
    public void Iban_RejectsTamperedCheckDigits()
    {
        Assert.False(PiiRedactor.IsValidIban("GB99WEST12345698765432"));
    }
}
