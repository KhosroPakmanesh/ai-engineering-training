public static class PromptComparer
{
    // Count words the same way for each summary so the results can be compared.
    public static async Task ComparePromptsAsync(List<(string Name, string File)> prompts)
    {
        foreach (var (name, promptFile) in prompts)
        {
            OutputPrinter.PrintPromptHeading(name);
            string summary = await ResumeSummarizer.SummarizeAsync(promptFile);

            int wordCount = summary.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;
            OutputPrinter.PrintSummary(summary, wordCount);
        }
    }
}
