public static class PromptComparer
{
    // Count words the same way for each summary so the results can be compared.
    public static async Task ComparePromptsAsync(List<(string Name, string File)> prompts)
    {
        foreach (var (name, promptFile) in prompts)
        {
            Console.WriteLine($"\n{name}");
            string summary = await ResumeSummarizer.SummarizeAsync(promptFile);

            Console.WriteLine(summary);
            Console.WriteLine($"Word count: {summary.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length}");
        }
    }
}
