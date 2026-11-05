public static class PromptComparer
{
    // Count words the same way for each summary so the results can be compared.
    public static async Task ComparePromptsAsync(
        List<PreparedPrompt> preparedPrompts,
        ProviderSettings providerSettings,
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        for (int indexInt = 0; indexInt < preparedPrompts.Count; indexInt++)
        {
            PreparedPrompt preparedPrompt = preparedPrompts[indexInt];
            OutputPrinter.PrintPromptHeading(
                indexInt + 1,
                preparedPrompt.NameString);
            string summaryString = await ResumeSummarizer.SummarizeAsync(
                preparedPrompt,
                providerSettings,
                httpClient,
                cancellationToken);

            int wordCountInt = summaryString.Split(
                [' ', '\t', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries).Length;
            OutputPrinter.PrintSummary(
                summaryString,
                wordCountInt);
        }
    }
}
