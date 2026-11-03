using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

public class ContextComparerTests(ITestOutputHelper outputITestOutputHelper)
{
    private static readonly JsonSerializerOptions JsonSerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

    // Each case makes two model calls; both cases make four, with up to twelve HTTP attempts.
    [Theory]
    [InlineData("Which project used PostgreSQL?", false)]
    [InlineData("Which project used Salesforce?", true)]
    [Trait("Category", "Integration")]
    public async Task Real_comparisons_cite_evidence_or_decline_when_it_is_missing(string questionString, bool unknownBool)
    {
        const string sourceFileString = "projects.md";
        ComparisonInputsValidator.ValidateQuestion(questionString);
        ComparisonInputs comparisonInputs = await ComparisonInputsLoader.LoadComparisonInputsAsync(questionString, sourceFileString);
        List<PreparedContext> preparedContexts = ContextCreator.CreateContextStrategies(comparisonInputs);
        ComparisonReport comparisonReport = await ContextComparer.CompareContextAsync(sourceFileString, comparisonInputs, preparedContexts);
        outputITestOutputHelper.WriteLine(JsonSerializer.Serialize(comparisonReport, JsonSerializerOptions));

        // Apply the same checks to both answers, then compare their prompt sizes.
        Assert.Equal(["FULL", "SELECTED"], comparisonReport.ContextResults.Select(contextResult => contextResult.StrategyString));
        foreach (ContextResult contextResult in comparisonReport.ContextResults)
        {
            AssertComparisonResult(contextResult, comparisonReport, unknownBool);
        }

        Assert.True(comparisonReport.ContextResults[1].PromptCharactersInt < comparisonReport.ContextResults[0].PromptCharactersInt);
    }

    // The real native requests must contain the original question and separate conversations.
    [Fact]
    public async Task Prepared_comparison_keeps_question_settings_and_independent_conversations()
    {
        const string questionString = "Which project used PostgreSQL?";
        ComparisonInputsValidator.ValidateQuestion(questionString);
        ComparisonInputs comparisonInputs = await ComparisonInputsLoader.LoadComparisonInputsAsync(questionString, "projects.md");
        List<PreparedContext> preparedContexts = ContextCreator.CreateContextStrategies(comparisonInputs);
        using var contextResponseHandler = new ContextResponseHandler("It states \"PostgreSQL stores stock levels and product records.\" [P1].");
        using var httpClient = new HttpClient(contextResponseHandler);
        ComparisonReport comparisonReport = await ContextComparer.CompareContextAsync("projects.md", comparisonInputs, preparedContexts,
            new("OpenRouter", "test-key", "test-model", new Uri("https://example.invalid/v1")), httpClient);
        Assert.Equal(2, contextResponseHandler.RequestBodyStrings.Count);
        // The existing report format keeps its original fields after C# member renames.
        JsonElement reportJsonElement = JsonSerializer.SerializeToElement(comparisonReport, JsonSerializerOptions.Web);
        Assert.Equal(new[]
        {
            "question", "sourceFile", "model", "temperature", "maxOutputTokens", "contextCharacterCap",
            "promptCharacterCap", "fullStrategy", "selectedStrategy", "tokenEstimateMethod", "results"
        }, reportJsonElement.EnumerateObject().Select(jsonProperty => jsonProperty.Name));
        Assert.All(
            comparisonReport.ContextResults,
            contextResult =>
        {
            Assert.Empty(contextResult.EvidenceFlagStrings);
            Assert.Null(contextResult.ProviderTokenUsage);
        });
        for (int indexInt = 0; indexInt < preparedContexts.Count; indexInt++)
        {
            using JsonDocument requestJsonDocument = JsonDocument.Parse(contextResponseHandler.RequestBodyStrings[indexInt]);
            JsonElement messagesJsonElement = requestJsonDocument.RootElement.GetProperty("messages");
            Assert.Equal(2, messagesJsonElement.GetArrayLength());
            Assert.Equal(preparedContexts[indexInt].PromptString, messagesJsonElement[1].GetProperty("content").GetString());
            using JsonDocument promptJsonDocument = JsonDocument.Parse(messagesJsonElement[1].GetProperty("content").GetString()!);
            Assert.Equal(questionString, promptJsonDocument.RootElement.GetProperty("question").GetString());
        }
    }

    // A preparation failure must keep both comparison requests away from the transport.
    [Fact]
    public async Task Invalid_question_prevents_all_remote_requests()
    {
        using var contextResponseHandler = new ContextResponseHandler("Unused response.");
        using var httpClient = new HttpClient(contextResponseHandler);
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            string questionString = new('x', 501);
            ComparisonInputsValidator.ValidateQuestion(questionString);
            ComparisonInputs comparisonInputs = await ComparisonInputsLoader.LoadComparisonInputsAsync(questionString, "projects.md");
            List<PreparedContext> preparedContexts = ContextCreator.CreateContextStrategies(comparisonInputs);
            await ContextComparer.CompareContextAsync("projects.md", comparisonInputs, preparedContexts,
                new("OpenRouter", "test-key", "test-model", new Uri("https://example.invalid/v1")), httpClient);
        });
        Assert.Empty(contextResponseHandler.RequestBodyStrings);
    }

    // Response completeness is checked before evidence or measurements can be accepted.
    [Theory]
    [InlineData("", "stop", "no response text")]
    [InlineData("Partial answer.", "length", "truncated")]
    public async Task Unusable_first_response_stops_the_comparison(string answerString, string finishReasonString, string expectedErrorString)
    {
        const string questionString = "Which project used PostgreSQL?";
        ComparisonInputsValidator.ValidateQuestion(questionString);
        ComparisonInputs comparisonInputs = await ComparisonInputsLoader.LoadComparisonInputsAsync(questionString, "projects.md");
        List<PreparedContext> preparedContexts = ContextCreator.CreateContextStrategies(comparisonInputs);
        using var contextResponseHandler = new ContextResponseHandler(answerString, finishReasonString);
        using var httpClient = new HttpClient(contextResponseHandler);
        InvalidDataException invalidDataException = await Assert.ThrowsAsync<InvalidDataException>(() =>
            ContextComparer.CompareContextAsync("projects.md", comparisonInputs, preparedContexts,
                new("OpenRouter", "test-key", "test-model", new Uri("https://example.invalid/v1")), httpClient));
        Assert.Contains(expectedErrorString, invalidDataException.Message);
        Assert.Single(contextResponseHandler.RequestBodyStrings);
    }

    // Expect supporting project IDs when an answer is available, or the uncertainty message when it is not.
    private static void AssertComparisonResult(ContextResult contextResult, ComparisonReport comparisonReport, bool unknownBool)
    {
        Assert.False(string.IsNullOrWhiteSpace(contextResult.ResponseString));
        Assert.Empty(contextResult.EvidenceFlagStrings);
        Assert.InRange(contextResult.ContextCharactersInt, 0, comparisonReport.ContextCharacterCapInt);
        Assert.InRange(contextResult.PromptCharactersInt, 1, comparisonReport.PromptCharacterCapInt);
        if (unknownBool)
        {
            Assert.Equal(ContextCreatorHelpers.UncertaintyResponseString, contextResult.ResponseString);
        }
        else
        {
            Assert.Contains("P1", contextResult.CitedSectionIdStrings);
            Assert.Equal(1.0, contextResult.SourceRecallDouble);
        }
    }
}
