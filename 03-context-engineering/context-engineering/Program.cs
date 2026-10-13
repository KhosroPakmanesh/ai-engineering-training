using System.ClientModel;
using System.Text.Json;

// Edit this question and run again. The application has no command-line options.
const string question = "Which project used PostgreSQL?";

try
{
    // Compare the same question with full and selected source context.
    ComparisonReport report = await ContextComparison.CompareAsync(question);

    // Show the measurements before saving the same report for later inspection.
    PrintComparison(report);
    await SaveComparisonAsync(report);
    return report.Results.All(result => result.EvidenceFlags.Length == 0) ? 0 : 1;
}
// An API failure stops the comparison before its existing saved report is replaced.
catch (ClientResultException error)
{
    OpenRouter.WriteError(error);
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

// Keep the question and strategy results in their original display order.
static void PrintComparison(ComparisonReport report)
{
    Console.WriteLine($"Question: {report.Question}");
    foreach (ContextResult result in report.Results)
        PrintResult(result);
}

// Prompt size, timing, recall, and evidence describe different aspects of the comparison.
static void PrintResult(ContextResult result)
{
    Console.WriteLine($"\n{result.Strategy}: {string.Join(", ", result.IncludedSectionIds)}");
    Console.WriteLine($"Prompt: {result.PromptCharacters} characters, ~{result.EstimatedPromptTokens} tokens (estimate)");
    Console.WriteLine($"Time: {result.LatencyMilliseconds} ms");
    Console.WriteLine(result.ProviderUsage is { } usage
        ? $"Provider tokens: input={usage.InputTokens?.ToString() ?? "unavailable"}, output={usage.OutputTokens?.ToString() ?? "unavailable"}"
        : "Provider token usage: unavailable");
    Console.WriteLine($"Source recall: {result.SourceRecall?.ToString("P0") ?? "not applicable"}");
    Console.WriteLine($"Evidence checks: {(result.EvidenceFlags.Length == 0 ? "passed" : string.Join("; ", result.EvidenceFlags))}");
    Console.WriteLine(result.Response);
}

// Save flagged comparisons too, so the learner can inspect why an evidence check failed.
static async Task SaveComparisonAsync(ComparisonReport report)
{
    string outputFile = Path.Combine(AppContext.BaseDirectory, "comparison.json");
    await File.WriteAllTextAsync(outputFile, JsonSerializer.Serialize(report, ContextComparison.JsonOptions));
    Console.WriteLine($"\nSaved comparison: {outputFile}");
}
