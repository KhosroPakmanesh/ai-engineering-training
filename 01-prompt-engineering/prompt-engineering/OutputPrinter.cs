using static ConsoleOutput;

public static class OutputPrinter
{
    // Name the changing input before the learner compares the summaries.
    public static void PrintIntroduction(int promptCount)
    {
        PrintHeading("Prompt Engineering: compare summary instructions");
        Console.WriteLine(
            $"Summarize the same fictional resume with {promptCount} different prompt instructions.\n" +
            "A: Zero-shot asks directly. B: Few-shot supplies examples. C: Constrained adds a 70-word limit.\n" +
            "The resume, system instruction, model, and generation settings stay fixed.\n" +
            "Each prompt starts an independent conversation, so earlier summaries cannot influence later ones.");
        PrintColoredLine(
            $"Preparing {promptCount} model calls, with up to {AiClientFactory.MaxRetries} HTTP retries per call.\n" +
            "These requests send the fictional resume and prompt text to the selected provider.",
            ConsoleColor.Gray);
    }

    // Label the case before its request so failures can be traced to the current prompt.
    public static void PrintPromptHeading(string name) => PrintHeading($"\n{name}");

    // Show the selected connection without exposing credentials.
    public static void PrintRequestDetails(ProviderSettings settings)
    {
        PrintColoredLine($"Model: {settings.Model}", ConsoleColor.Gray);
        PrintColoredLine($"Provider: {settings.Name}", ConsoleColor.Gray);
    }

    // Show the original answer beside the word count supplied by the comparison stage.
    public static void PrintSummary(string summary, int wordCount)
    {
        Console.WriteLine(summary);
        PrintColoredLine($"Word count: {wordCount}", ConsoleColor.Gray);
    }

    // A short answer can still contain a wrong fact, so the learner needs to inspect the resume.
    public static void PrintTakeaway()
    {
        PrintHeading("\nWhat to take away");
        Console.WriteLine(
            "Instructions and examples can change which facts the model selects and how it writes them.\n" +
            "The word count splits on spaces, tabs, and line breaks; compare C with its 70-word limit.\n" +
            "The app rejects empty or cut-short answers. Read each summary to check its facts and paragraph format.\n" +
            "For B, check that facts from the examples did not enter the candidate's summary.\n" +
            "Change one prompt, keep the other inputs fixed, and run again to compare the effect.");
    }
}
