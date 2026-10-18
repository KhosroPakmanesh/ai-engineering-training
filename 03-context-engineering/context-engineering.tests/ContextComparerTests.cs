using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

public class ContextComparerTests(ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    // Each case makes two model calls; both cases make four, with up to twelve HTTP attempts.
    [Theory]
    [InlineData("Which project used PostgreSQL?", false)]
    [InlineData("Which project used Salesforce?", true)]
    [Trait("Category", "Integration")]
    public async Task Real_comparisons_cite_evidence_or_decline_when_it_is_missing(string question, bool unknown)
    {
        const string sourceFile = "projects.md";
        ComparisonInputs inputs = await ComparisonInputsLoader.LoadComparisonInputsAsync(question, sourceFile);
        List<PreparedContext> preparedContexts = ContextCreator.CreateContextStrategies(inputs);
        ComparisonReport report = await ContextComparer.CompareContextAsync(sourceFile, inputs, preparedContexts);
        output.WriteLine(JsonSerializer.Serialize(report, JsonOptions));

        // Apply the same checks to both answers, then compare their prompt sizes.
        Assert.Equal(["FULL", "SELECTED"], report.Results.Select(result => result.Strategy));
        foreach (ContextResult result in report.Results)
            AssertComparisonResult(result, report, unknown);
        Assert.True(report.Results[1].PromptCharacters < report.Results[0].PromptCharacters);
    }

    // Expect supporting project IDs when an answer is available, or the uncertainty message when it is not.
    private static void AssertComparisonResult(ContextResult result, ComparisonReport report, bool unknown)
    {
        Assert.False(string.IsNullOrWhiteSpace(result.Response));
        Assert.Empty(result.EvidenceFlags);
        Assert.InRange(result.ContextCharacters, 0, report.ContextCharacterCap);
        Assert.InRange(result.PromptCharacters, 1, report.PromptCharacterCap);
        if (unknown) Assert.Equal(ContextCreatorHelpers.UncertaintyResponse, result.Response);
        else
        {
            Assert.Contains("P1", result.CitedSectionIds);
            Assert.Equal(1.0, result.SourceRecall);
        }
    }
}
