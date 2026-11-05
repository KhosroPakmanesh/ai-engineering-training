using System.Text.Json;
using Xunit;

public class PromptComparerTests
{
    // A bad final case must prevent even the first paid request through the full preparation sequence.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Invalid_later_case_sends_zero_requests(bool missingBool)
    {
        // Arrange
        using var summaryResponseHandler = new SummaryResponseHandler();
        using var httpClient = new HttpClient(summaryResponseHandler);

        string emptyPathString = Path.GetTempFileName();

        // Act
        // Assert
        try
        {
            List<PromptFile> promptFiles = PromptTestData.CreatePromptFiles();
            promptFiles[^1] = new(
                "Invalid",
                missingBool ? "Prompts/missing.txt" : emptyPathString);
            Exception? exception = await Record.ExceptionAsync(async () =>
            {
                ComparisonInputs comparisonInputs = await ComparisonInputsLoader.LoadAsync(promptFiles);
                InputValidator.Validate(comparisonInputs);
                List<PreparedPrompt> preparedPrompts = PromptPreparer.Prepare(comparisonInputs);
                await PromptComparer.ComparePromptsAsync(
                    preparedPrompts,
                    new(
                        "OpenRouter",
                        "test-key",
                        "test-model",
                        new Uri("https://example.invalid/v1")),
                    httpClient);
            });
            if (missingBool)
            {
                Assert.IsType<FileNotFoundException>(exception);
            }
            else
            {
                Assert.Contains(
                    "prompt instruction",
                    Assert.IsType<InvalidDataException>(exception).Message);
            }
            Assert.Empty(summaryResponseHandler.RequestBodyStrings);
        }
        finally
        {
            File.Delete(emptyPathString);
        }
    }

    // Fixed prepared text reaches three independent native requests without rereading input files.
    [Fact]
    public async Task Comparison_sends_each_prepared_prompt_in_an_independent_conversation()
    {
        // Arrange
        List<PreparedPrompt> preparedPrompts = await PromptTestData.PrepareAsync();
        using var summaryResponseHandler = new SummaryResponseHandler();
        using var httpClient = new HttpClient(summaryResponseHandler);

        // Act
        await PromptComparer.ComparePromptsAsync(
            preparedPrompts,
            new(
                "OpenRouter",
                "test-key",
                "test-model",
                new Uri("https://example.invalid/v1")),
            httpClient);

        // Assert
        Assert.Equal(
            3,
            summaryResponseHandler.RequestBodyStrings.Count);
        for (int indexInt = 0; indexInt < preparedPrompts.Count; indexInt++)
        {
            using JsonDocument requestJsonDocument =
                JsonDocument.Parse(summaryResponseHandler.RequestBodyStrings[indexInt]);
            JsonElement messagesJsonElement = requestJsonDocument.RootElement.GetProperty("messages");
            Assert.Equal(
                2,
                messagesJsonElement.GetArrayLength());
            Assert.Equal(
                preparedPrompts[indexInt].TextString,
                messagesJsonElement[1].GetProperty("content").GetString());
        }
    }
}
