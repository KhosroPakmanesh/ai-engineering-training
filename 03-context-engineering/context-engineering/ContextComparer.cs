using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

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

        OutputPrinter.PrintRequestDetails(settings);

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
        var factory = new AiClientFactory();
        using IChatClient client = factory.CreateClient(settings);
        ChatClientAgent agent = new(client, new ChatClientAgentOptions
        {
            Name = "ResumeContext",
            ChatOptions = new ChatOptions
            {
                Instructions = ContextCreatorHelpers.SystemInstructions,
                Temperature = Temperature,
                MaxOutputTokens = MaxOutputTokens
            }
        });

        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(AiClientFactory.RequestTimeoutSeconds));

        var timer = Stopwatch.StartNew();
        AgentResponse response = await agent.RunAsync(context.Prompt, cancellationToken: deadline.Token);
        timer.Stop();

        // Preserve both comparison results only when each response is present and complete.
        if (response.FinishReason == ChatFinishReason.Length)
            throw new InvalidDataException("The provider truncated its response at the output token limit.");
        string? answer = response.Messages.LastOrDefault(message => message.Role == ChatRole.Assistant)?.Text;
        if (string.IsNullOrWhiteSpace(answer))
            throw new InvalidDataException(
                "The provider returned no response text. Check the model and its reasoning/output token budget.");

        return CreateResult(context, answer, response.Usage, timer.ElapsedMilliseconds, expectedIds);
    }
    // Keep failed evidence checks with the answer so both results can still be compared.
    private static ContextResult CreateResult(
        PreparedContext prepared, string responseText, UsageDetails? usage,
        long latencyMilliseconds, List<string>? expectedIds)
    {
        ContextStrategy context = prepared.Context;
        string answer = responseText.Trim();

        // Missing measurements mean unknown, even when the framework returns an empty usage object.
        TokenUsage? providerUsage = usage is null ||
            (usage.InputTokenCount is null && usage.OutputTokenCount is null && usage.TotalTokenCount is null)
            ? null : new(usage.InputTokenCount, usage.OutputTokenCount, usage.TotalTokenCount);

        List<string> includedIds = context.Sections.Select(section => section.Id).ToList();
        int promptCharacters = ContextCreatorHelpers.SystemInstructions.Length + prepared.Prompt.Length;

        return new(
            Strategy: context.Strategy,
            IncludedSectionIds: includedIds,
            ContextCharacters: context.Text.Length,
            PromptCharacters: promptCharacters,
            EstimatedPromptTokens: (int)Math.Ceiling(promptCharacters / 4.0),
            ProviderUsage: providerUsage,
            LatencyMilliseconds: latencyMilliseconds,
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
