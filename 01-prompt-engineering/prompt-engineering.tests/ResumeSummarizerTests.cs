using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

public class ResumeSummarizerTests(ITestOutputHelper output)
{
    // Each case makes one model call, with up to three HTTP attempts, using the shared settings and inputs.
    [Theory]
    [InlineData("Prompts/A-zero-shot.txt", null)]
    [InlineData("Prompts/B-few-shot.txt", null)]
    [InlineData("Prompts/C-constrained.txt", 70)]
    [Trait("Category", "Integration")]
    public async Task Prompts_return_one_paragraph_and_obey_word_limit(string promptFile, int? wordLimit)
    {
        // Check the whole comparison before any case can spend a paid model request.
        List<string> comparisonPromptFiles =
        [
            "Prompts/A-zero-shot.txt",
            "Prompts/B-few-shot.txt",
            "Prompts/C-constrained.txt"
        ];
        await InputValidator.ValidateInputsAsync(comparisonPromptFiles);

        string summary = await ResumeSummarizer.SummarizeAsync(promptFile);
        int wordCount = summary.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;
        output.WriteLine(summary);
        output.WriteLine($"Word count: {wordCount}");

        // Check paragraph and word limits because the model can choose different wording.
        Assert.False(string.IsNullOrWhiteSpace(summary), "Expected a nonempty summary.");
        Assert.False(Regex.IsMatch(summary.Trim(), @"\n\s*\n"), "Expected one paragraph, without blank lines between paragraphs.");
        if (wordLimit.HasValue)
            Assert.InRange(wordCount, 1, wordLimit.Value);
    }
}
