public static class PromptComparer
{
    // Show each independent answer beside the same word-count measurement.
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
