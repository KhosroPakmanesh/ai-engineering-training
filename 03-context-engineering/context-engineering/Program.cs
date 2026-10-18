using System.ClientModel;

const string question = "Which project used PostgreSQL?";
const string sourceFile = "projects.md";

try
{
    // Reject an unusable question before loading inputs or making model requests.
    if (string.IsNullOrWhiteSpace(question) || question.Length > 500)
        throw new ArgumentException("The question must contain between 1 and 500 characters.", nameof(question));

    // Expected source IDs measure whether each context includes the needed evidence.
    ComparisonInputs comparisonInputs = await ComparisonInputsLoader
        .LoadComparisonInputsAsync(question, sourceFile);

    // Prepare and size-check both prompts before sending the first request.
    List<PreparedContext> preparedContexts =
        ContextCreator.CreateContextStrategies(comparisonInputs);

    // Change only the supplied context while keeping the question and settings fixed.
    ComparisonReport comparisonReport = await ContextComparer.
        CompareContextAsync(sourceFile,comparisonInputs, preparedContexts);

    // Keep the answers beside their prompt sizes, timing, and local evidence flags.
    Console.WriteLine($"Question: {comparisonReport.Question}");
    foreach (ContextResult result in comparisonReport.Results)
    {
        Console.WriteLine($"\n{result.Strategy}: {string.Join(", ", result.IncludedSectionIds)}");
        Console.WriteLine($"Prompt: {result.PromptCharacters} characters, ~{result.EstimatedPromptTokens} tokens (estimate)");
        Console.WriteLine($"Time: {result.LatencyMilliseconds} ms");
        Console.WriteLine(result.ProviderUsage is { } usage
            ? $"Provider tokens: input={usage.InputTokens?.ToString() ?? "unavailable"}, output={usage.OutputTokens?.ToString() ?? "unavailable"}"
            : "Provider token usage: unavailable");
        Console.WriteLine($"Source recall: {result.SourceRecall?.ToString("P0") ?? "not applicable"}");
        Console.WriteLine($"Evidence checks: {(result.EvidenceFlags.Count == 0 ? "passed" : string.Join("; ", result.EvidenceFlags))}");
        Console.WriteLine(result.Response);
    }

    return comparisonReport.Results.All(result => result.EvidenceFlags.Count == 0) ? 0 : 1;
}
catch (ClientResultException error)
{
    AiProvider.WriteError(error);
    return 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("The request was canceled or exceeded the 120-second time limit.");
    return 1;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
