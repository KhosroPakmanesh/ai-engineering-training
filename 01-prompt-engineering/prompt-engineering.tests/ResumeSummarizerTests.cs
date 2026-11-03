using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

public class ResumeSummarizerTests(ITestOutputHelper outputITestOutputHelper)
{
    // Each case makes one model call, with up to three HTTP attempts, using the shared settings and inputs.
    [Theory]
    [InlineData("Prompts/A-zero-shot.txt", null)]
    [InlineData("Prompts/B-few-shot.txt", null)]
    [InlineData("Prompts/C-constrained.txt", 70)]
    [Trait("Category", "Integration")]
    public async Task Prompts_return_one_paragraph_and_obey_word_limit(string promptFileString, int? wordLimitInt)
    {
        // Check the whole comparison before any case can spend a paid model request.
        List<PreparedPrompt> preparedPrompts = await PromptTestData.PrepareAsync();
        ProviderSettings providerSettings = await SettingsReader.ReadSettingsAsync(AppContext.BaseDirectory);
        PreparedPrompt preparedPrompt = preparedPrompts.Single(preparedPrompt => preparedPrompt.PathString == promptFileString);
        string summaryString = await ResumeSummarizer.SummarizeAsync(preparedPrompt, providerSettings);
        int wordCountInt = summaryString.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;
        outputITestOutputHelper.WriteLine(summaryString);
        outputITestOutputHelper.WriteLine($"Word count: {wordCountInt}");

        // Check paragraph and word limits because the model can choose different wording.
        Assert.False(string.IsNullOrWhiteSpace(summaryString), "Expected a nonempty summary.");
        Assert.False(Regex.IsMatch(summaryString.Trim(), @"\n\s*\n"), "Expected one paragraph, without blank lines between paragraphs.");
        if (wordLimitInt.HasValue)
        {
            Assert.InRange(wordCountInt, 1, wordLimitInt.Value);
        }
    }

    // Native response checks reject incomplete answers without depending on a remote model.
    [Theory]
    [InlineData("", "stop", "no response text")]
    [InlineData("Partial summary.", "length", "truncated")]
    public async Task Empty_and_truncated_answers_are_rejected(string answerString, string finishReasonString, string expectedErrorString)
    {
        List<PreparedPrompt> preparedPrompts = await PromptTestData.PrepareAsync();
        using var summaryResponseHandler = new SummaryResponseHandler(answerString, finishReasonString);
        using var httpClient = new HttpClient(summaryResponseHandler);
        InvalidDataException invalidDataException = await Assert.ThrowsAsync<InvalidDataException>(() =>
            ResumeSummarizer.SummarizeAsync(preparedPrompts[0],
                new("OpenRouter", "test-key", "test-model", new Uri("https://example.invalid/v1")), httpClient));
        Assert.Contains(expectedErrorString, invalidDataException.Message);
        Assert.Single(summaryResponseHandler.RequestBodyStrings);
    }

    // Caller cancellation stops both a pending request and a request canceled before HTTP begins.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancellation_stops_native_requests(bool cancelBeforeBool)
    {
        List<PreparedPrompt> preparedPrompts = await PromptTestData.PrepareAsync();
        using var cancellationTokenSource = new CancellationTokenSource();
        using var summaryResponseHandler = new SummaryResponseHandler(beforeResponseAction: cancellationTokenSource.Cancel);
        using var httpClient = new HttpClient(summaryResponseHandler);
        if (cancelBeforeBool)
        {
            cancellationTokenSource.Cancel();
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ResumeSummarizer.SummarizeAsync(preparedPrompts[0],
            new("OpenRouter", "test-key", "test-model", new Uri("https://example.invalid/v1")), httpClient, cancellationTokenSource.Token));
        Assert.Equal(cancelBeforeBool ? 0 : 1, summaryResponseHandler.RequestBodyStrings.Count);
    }
}
