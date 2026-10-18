using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

public static partial class ContextComparer
{
    private const float Temperature = 0.2f;
    private const int MaxOutputTokens = 4_096;

    // Reuse one model and settings while sending each prepared context independently.
    public static async Task<ComparisonReport> CompareContextAsync
        (string sourceFile, ComparisonInputs comparisonInputs,
        List<PreparedContext> preparedContexts, CancellationToken cancellationToken = default)
    {
        string folder = AppContext.BaseDirectory;
        ProviderSettings settings = await SettingsReader.ReadSettingsAsync(folder, cancellationToken);

        using IChatClient chatClient = AiProvider.CreateChatClient(settings).AsIChatClient();
        ChatClientAgent agent = CreateAgent(chatClient);

        Console.WriteLine(
            $"Model: {settings.Model}. Sending fictional project excerpts to " +
            $"{settings.Name} (two model calls, up to {AiRetryPolicy.MaxRetries} retries per call).");

        var results = new List<ContextResult>();
        foreach (PreparedContext preparedContext in preparedContexts)
        {
            results.Add(await RunContextAsync(agent, preparedContext, comparisonInputs.ExpectedIds, cancellationToken));
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
    // Both requests receive the same evidence rules and generation settings.
    private static ChatClientAgent CreateAgent(IChatClient chatClient) => new(chatClient, new ChatClientAgentOptions
    {
        Name = "ResumeContext",
        ChatOptions = new()
        {
            Instructions = ContextCreatorHelpers.SystemInstructions,
            Temperature = Temperature,
            MaxOutputTokens = MaxOutputTokens
        }
    });
    // A fresh run prevents earlier answers from becoming extra context.
    private static async Task<ContextResult> RunContextAsync(
        ChatClientAgent agent, PreparedContext context, List<string>? expectedIds, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(AiProvider.RequestTimeoutSeconds));
        var timer = Stopwatch.StartNew();

        AgentResponse response = await agent.RunAsync(context.Prompt, cancellationToken: timeout.Token);
        timer.Stop();

        return CreateResult(context, response, timer.ElapsedMilliseconds, expectedIds);
    }
    // Retain evidence flags with each answer so one flagged result does not stop comparison.
    private static ContextResult CreateResult(
    PreparedContext prepared, AgentResponse response, long elapsedMilliseconds, List<string>? expectedIds)
    {
        ContextStrategy context = prepared.Context;
        string answer = AiProvider.ReadResponse(response.Text,
            response.RawRepresentation is ChatResponse chatResponse
            && chatResponse.FinishReason == ChatFinishReason.Length).Trim();

        List<string> includedIds = context.Sections.Select(section => section.Id).ToList();
        int promptCharacters = ContextCreatorHelpers.SystemInstructions.Length + prepared.Prompt.Length;

        return new(
            Strategy: context.Strategy,
            IncludedSectionIds: includedIds,
            ContextCharacters: context.Text.Length,
            PromptCharacters: promptCharacters,
            EstimatedPromptTokens: (int)Math.Ceiling(promptCharacters / 4.0),
            ProviderUsage: ReadProviderUsage(response.Usage),
            LatencyMilliseconds: elapsedMilliseconds,
            SourceRecall: CalculateSourceRecall(includedIds, expectedIds),
            Response: answer,
            CitedSectionIds: ContextEvidenceChecker.CitedIds(answer),
            EvidenceFlags: ContextEvidenceChecker.CheckEvidence(answer, context.Sections, expectedIds));
    }
    // Missing provider measurements stay null rather than being reported as zero tokens.
    private static TokenUsage? ReadProviderUsage(UsageDetails? usage) => usage is null ||
        (usage.InputTokenCount is null && usage.OutputTokenCount is null && usage.TotalTokenCount is null)
        ? null : new(usage.InputTokenCount, usage.OutputTokenCount, usage.TotalTokenCount);
    // Recall measures source inclusion, independently of the quality of the model's answer.
    private static double? CalculateSourceRecall(List<string> includedIds, List<string>? expectedIds) =>
        expectedIds is { Count: > 0 }
            ? (double)expectedIds.Count(id => includedIds.Contains(id, StringComparer.Ordinal)) / expectedIds.Count
            : null;
}
