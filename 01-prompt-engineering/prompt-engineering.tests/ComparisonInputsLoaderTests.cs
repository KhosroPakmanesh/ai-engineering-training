using Xunit;

public class ComparisonInputsLoaderTests
{
    // Missing later files must be found while the whole comparison is still being loaded.
    [Fact]
    public async Task Missing_later_prompt_is_rejected_during_loading()
    {
        // Arrange
        List<PromptFile> promptFiles = PromptTestData.CreatePromptFiles();
        promptFiles[^1] = new(
            "Missing",
            "Prompts/does-not-exist.txt");

        // Act
        // Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => ComparisonInputsLoader.LoadAsync(promptFiles));
    }
}
