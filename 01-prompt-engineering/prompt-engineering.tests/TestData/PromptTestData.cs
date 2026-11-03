public static class PromptTestData
{
    // Fresh file records keep each acceptance test independent of other comparisons.
    public static List<PromptFile> CreatePromptFiles()
    {
        return
        [
            new("A: Zero-shot", "Prompts/A-zero-shot.txt"),
            new("B: Few-shot", "Prompts/B-few-shot.txt"),
            new("C: Constrained", "Prompts/C-constrained.txt")
        ];
    }

    // Complete the same loading, validation, and preparation sequence as Program.cs.
    public static async Task<List<PreparedPrompt>> PrepareAsync()
    {
        ComparisonInputs comparisonInputs = await ComparisonInputsLoader.LoadAsync(CreatePromptFiles());
        InputValidator.Validate(comparisonInputs);
        return PromptPreparer.Prepare(comparisonInputs);
    }
}
