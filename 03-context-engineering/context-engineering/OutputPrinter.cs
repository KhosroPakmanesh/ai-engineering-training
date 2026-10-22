using static ConsoleOutput;

public static class OutputPrinter
{
    // Explain context selection before sending either of the prepared prompts.
    public static void PrintIntroduction(string question, string sourceFile)
    {
        PrintHeading("Context Engineering: choose what the model can read");
        Console.WriteLine($"Question: {question}\nSource: SampleData/{sourceFile}");
        PrintColoredLine("FULL supplies all project descriptions within the size limit.", ConsoleColor.Cyan);
        PrintColoredLine("SELECTED uses question words to choose up to two matching descriptions.", ConsoleColor.DarkYellow);
        Console.WriteLine(
            "Only the supplied descriptions change. The question, instructions, model, and settings stay fixed.\n" +
            "Both prompts are prepared before the first request, and each starts an independent conversation.");
    }

    // Report the two-call plan without taking ownership of the model requests.
    public static void PrintRequestDetails(ProviderSettings settings) =>
        PrintColoredLine(
            $"Model: {settings.Model}. Sending fictional project excerpts to " +
            $"{settings.Name} (two model calls, up to {AiClientFactory.MaxRetries} retries per call).",
            ConsoleColor.Gray);

    // Display stored measurements and evidence flags without recalculating or accepting the results.
    public static void PrintResults(ComparisonReport comparisonReport)
    {
        PrintHeading("\nContext comparison");
        Console.WriteLine($"Question: {comparisonReport.Question}");
        foreach (ContextResult result in comparisonReport.Results)
        {
            ConsoleColor strategyColor = result.Strategy == "FULL" ? ConsoleColor.Cyan : ConsoleColor.DarkYellow;
            PrintColoredLine($"\n{result.Strategy}: {string.Join(", ", result.IncludedSectionIds)}", strategyColor);
            Console.WriteLine($"Prompt: {result.PromptCharacters} characters, ~{result.EstimatedPromptTokens} tokens (estimate)");
            Console.WriteLine($"Time: {result.LatencyMilliseconds} ms");
            Console.WriteLine(result.ProviderUsage is { } usage
                ? $"Provider tokens: input={usage.InputTokens?.ToString() ?? "unavailable"}, output={usage.OutputTokens?.ToString() ?? "unavailable"}"
                : "Provider token usage: unavailable");
            Console.WriteLine($"Source recall: {result.SourceRecall?.ToString("P0") ?? "not applicable"}");
            Console.WriteLine($"Evidence checks: {(result.EvidenceFlags.Count == 0 ? "passed" : string.Join("; ", result.EvidenceFlags))}");
            Console.WriteLine(result.Response);
        }
    }

    // Smaller context helps only when it keeps the evidence needed to answer the question.
    public static void PrintTakeaway()
    {
        PrintHeading("\nWhat to take away");
        Console.WriteLine(
            "Compare the supplied project IDs, prompt sizes, answers, and evidence flags.\n" +
            "Token estimates divide characters by four and round up; provider counts stay unavailable when not reported.\n" +
            "Source recall measures included expected project IDs, not answer quality; it is not applicable without them.\n" +
            "Citation and quote checks cannot establish that the answer interprets the source correctly.\n" +
            "Read SampleData/projects.md, change only the question, and run again.\n" +
            "Does SELECTED retain the evidence needed to answer while using less context?");
    }
}
