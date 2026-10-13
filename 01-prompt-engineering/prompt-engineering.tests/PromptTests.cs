using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

public class PromptTests(ITestOutputHelper output)
{
    // Each case makes one real API request using the application's .env and data files.
    [Theory]
    [InlineData("prompts/A-zero-shot.txt", null)]
    [InlineData("prompts/B-few-shot.txt", null)]
    [InlineData("prompts/C-constrained.txt", 70)]
    [Trait("Category", "Integration")]
    public async Task Prompts_return_one_paragraph_and_obey_word_limit(string promptFile, int? wordLimit)
    {
        string summary = await ResumeSummarizer.SummarizeAsync(promptFile);
        int wordCount = summary.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;
        output.WriteLine(summary);
        output.WriteLine($"Word count: {wordCount}");

        // Assert observable constraints rather than one expected model wording.
        Assert.False(string.IsNullOrWhiteSpace(summary), "Expected a nonempty summary.");
        Assert.False(Regex.IsMatch(summary.Trim(), @"\n\s*\n"), "Expected one paragraph, without blank lines between paragraphs.");
        if (wordLimit.HasValue)
            Assert.InRange(wordCount, 1, wordLimit.Value);
    }

    // Preparation should discover a missing prompt before the comparison can spend a request.
    [Fact]
    public async Task Missing_prompt_is_rejected_during_preparation_without_an_api_request()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            ResumeSummarizer.ValidateInputsAsync(["prompts/does-not-exist.txt"]));
    }
}
