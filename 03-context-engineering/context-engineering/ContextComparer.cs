public static partial class ContextComparer
{
    private const float Temperature = 0.2f;
    private const int MaxOutputTokens = 4_096;

    // Use the same model and settings for both contexts, with a separate conversation for each.
    public static async Task<ComparisonReport> CompareContextAsync
        (string sourceFile, ComparisonInputs comparisonInputs,
        List<PreparedContext> preparedContexts, CancellationToken cancellationToken = default)
    {
        string folder = AppContext.BaseDirectory;
        ProviderSettings settings = await SettingsReader.ReadSettingsAsync(folder, cancellationToken);

        Console.WriteLine(
            $"Model: {settings.Model}. Sending fictional project excerpts to " +
            $"{settings.Name} (two model calls, up to {AiProvider.MaxRetries} retries per call).");

        var results = new List<ContextResult>();
        foreach (PreparedContext preparedContext in preparedContexts)
        {
            results.Add(await RunContextAsync(settings, preparedContext, comparisonInputs.ExpectedIds, cancellationToken));
        }

        return new(
            Question: comparisonInputs.Question,
            SourceFile: sourceFile,
            Model: settings.Model,
            Temperature: Temperature,
            MaxOutputTokens: MaxOutputTokens,
            ContextCharacterCap: ContextCreatorHelpers.ContextCharacterCap,
            PromptCharacterCap: ContextCreatorHelpers.PromptCharacterCap,
            FullStrategy: FullContextStrategy.StrategyDescription,
            SelectedStrategy: SelectedContextStrategy.StrategyDescription,
            TokenEstimateMethod:
                "Estimate = ceiling(system and user prompt characters / 4). " +
                "Excludes message framing; not a model tokenizer. Provider usage is null when unavailable.",
            Results: results);
    }
    // Send only this context so the model cannot use an earlier answer.
    private static async Task<ContextResult> RunContextAsync(
        ProviderSettings settings, PreparedContext context, List<string>? expectedIds, CancellationToken cancellationToken)
    {
        AiRequest request = new(
            Name: "ResumeContext",
            Instructions: ContextCreatorHelpers.SystemInstructions,
            Input: context.Prompt,
            Temperature: Temperature,
            MaxOutputTokens: MaxOutputTokens);

        AiResponse response = await AiProvider
            .RequestAsync(settings, request, cancellationToken);

        return CreateResult(context, response, expectedIds);
    }
    // Keep failed evidence checks with the answer so both results can still be compared.
    private static ContextResult CreateResult(
    PreparedContext prepared, AiResponse response, List<string>? expectedIds)
    {
        ContextStrategy context = prepared.Context;
        string answer = response.Text.Trim();

        List<string> includedIds = context.Sections.Select(section => section.Id).ToList();
        int promptCharacters = ContextCreatorHelpers.SystemInstructions.Length + prepared.Prompt.Length;

        return new(
            Strategy: context.Strategy,
            IncludedSectionIds: includedIds,
            ContextCharacters: context.Text.Length,
            PromptCharacters: promptCharacters,
            EstimatedPromptTokens: (int)Math.Ceiling(promptCharacters / 4.0),
            ProviderUsage: response.Usage is { } usage ? new(usage.InputTokens, usage.OutputTokens, usage.TotalTokens) : null,
            LatencyMilliseconds: response.LatencyMilliseconds,
            SourceRecall: CalculateSourceRecall(includedIds, expectedIds),
            Response: answer,
            CitedSectionIds: ContextEvidenceChecker.CitedIds(answer),
            EvidenceFlags: ContextEvidenceChecker.CheckEvidence(answer, context.Sections, expectedIds));
    }
    // Source recall is the share of expected project IDs included in the context; it does not rate the answer.
    private static double? CalculateSourceRecall(List<string> includedIds, List<string>? expectedIds) =>
        expectedIds is { Count: > 0 }
            ? (double)expectedIds.Count(id => includedIds.Contains(id, StringComparer.Ordinal)) / expectedIds.Count
            : null;
}
