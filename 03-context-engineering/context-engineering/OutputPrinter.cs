using static ConsoleOutput;

public static class OutputPrinter
{
    // Explain context selection before sending either of the prepared prompts.
    public static void PrintIntroduction(string questionString, string sourceFileString)
    {
        PrintHeading("Context Engineering: choose what the model can read");
        PrintColoredLine($"QUESTION | {questionString}", ConsoleColor.Cyan);
        Console.WriteLine($"Source: SampleData/{sourceFileString}");
        PrintColoredLine("FULL supplies all project descriptions within the size limit.", ConsoleColor.Cyan);
        PrintColoredLine("SELECTED uses question words to choose up to two matching descriptions.", ConsoleColor.Cyan);
        Console.WriteLine(
            "Only the supplied descriptions change. The question, instructions, model, and settings stay fixed.\n" +
            "Both prompts are prepared before the first request, and each starts an independent conversation.");
    }

    // Report the two-call plan without taking ownership of the model requests.
    public static void PrintRequestDetails(ProviderSettings providerSettings)
    {
        PrintColoredLine(
            $"Model: {providerSettings.ModelString}. Sending fictional project excerpts to " +
            $"{providerSettings.NameString} (two model calls, up to {AiClientFactory.MaxRetries} retries per call).",
            ConsoleColor.Gray);
    }

    // Display stored measurements and evidence flags without recalculating or accepting the results.
    public static void PrintResults(ComparisonReport comparisonReport, List<ProjectSection> projectSections)
    {
        PrintHeading("\nContext comparison");
        PrintColoredLine($"QUESTION | {comparisonReport.QuestionString}", ConsoleColor.Cyan);
        for (int indexInt = 0; indexInt < comparisonReport.ContextResults.Count; indexInt++)
        {
            ContextResult contextResult = comparisonReport.ContextResults[indexInt];
            PrintColoredLine($"\n── Answer {indexInt + 1} ──────────────────────────", ConsoleColor.White);
            PrintColoredLine($"CONTEXT | {contextResult.StrategyString}: {string.Join(", ", contextResult.IncludedSectionIdStrings)}",
                ConsoleColor.Cyan);
            Console.WriteLine($"Prompt: {contextResult.PromptCharactersInt} characters, ~{contextResult.EstimatedPromptTokensInt} tokens (estimate)");
            Console.WriteLine($"Time: {contextResult.LatencyMillisecondsLong} ms");
            PrintColoredLine(contextResult.ProviderTokenUsage is { } tokenUsage
                ? $"Provider tokens: input={tokenUsage.InputTokensLong?.ToString() ?? "unavailable"}, output={tokenUsage.OutputTokensLong?.ToString() ?? "unavailable"}"
                : "Provider token usage: unavailable", ConsoleColor.Gray);
            Console.WriteLine($"Source recall: {contextResult.SourceRecallDouble?.ToString("P0") ?? "not applicable"}");
            PrintColoredLine(
                $"STATUS | {(contextResult.EvidenceFlagStrings.Count == 0 ? "evidence checks passed" : string.Join("; ", contextResult.EvidenceFlagStrings))}",
                contextResult.EvidenceFlagStrings.Count == 0 ? ConsoleColor.Cyan : ConsoleColor.DarkYellow);
            Console.WriteLine(contextResult.ResponseString);

            if (contextResult.EvidenceFlagStrings.Count > 0)
            {
                PrintColoredLine("Accepted citations: unavailable because evidence checks failed.", ConsoleColor.DarkYellow);
            }
            else if (contextResult.CitedSectionIdStrings.Count == 0)
            {
                PrintColoredLine("Accepted citations: none; the answer reports insufficient evidence.", ConsoleColor.DarkYellow);
            }
            else
            {
                Console.WriteLine("Accepted citations:");
                foreach (string idString in contextResult.CitedSectionIdStrings)
                {
                    ProjectSection projectSection = projectSections.Single(projectSection => projectSection.IdString == idString);
                    PrintColoredLine($"SOURCE | [{idString}] {projectSection.HeadingString}", ConsoleColor.Cyan);
                    Console.WriteLine(projectSection.TextString);
                }
            }
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
