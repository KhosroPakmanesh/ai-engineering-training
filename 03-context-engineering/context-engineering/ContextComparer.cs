using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

public static partial class ContextComparer
{
    private const float TemperatureFloat = 0.2f;
    private const int MaxOutputTokensInt = 4_096;

    // Use the same model and settings for both contexts, with a separate conversation for each.
    public static async Task<ComparisonReport> CompareContextAsync(
        string sourceFileString,
        ComparisonInputs comparisonInputs,
        List<PreparedContext> preparedContexts,
        CancellationToken cancellationToken = default)
    {
        string folderString = AppContext.BaseDirectory;
        ProviderSettings providerSettings = await SettingsReader.ReadSettingsAsync(
            folderString,
            cancellationToken);

        return await CompareContextAsync(
            sourceFileString,
            comparisonInputs,
            preparedContexts,
            providerSettings,
            httpClient: null,
            cancellationToken);
    }

    // The same prepared comparison can use an injected HTTP boundary in deterministic tests.
    internal static async Task<ComparisonReport> CompareContextAsync(
        string sourceFileString,
        ComparisonInputs comparisonInputs,
        List<PreparedContext> preparedContexts,
        ProviderSettings providerSettings,
        HttpClient? httpClient,
        CancellationToken cancellationToken = default)
    {

        OutputPrinter.PrintRequestDetails(providerSettings);

        var contextResults = new List<ContextResult>();
        foreach (PreparedContext preparedContext in preparedContexts)
        {
            // Measure only the native request, then inspect the returned answer in separate stages.
            var stopwatch = Stopwatch.StartNew();
            AgentResponse agentResponse = await RequestAsync(
                providerSettings,
                preparedContext,
                httpClient,
                cancellationToken);
            stopwatch.Stop();
            string answerString = agentResponse.Messages.Last(chatMessage =>
                chatMessage.Role == ChatRole.Assistant).Text.Trim();
            List<string> citedIdStrings = ContextEvidenceChecker.CitedIds(answerString);
            List<string> evidenceFlagStrings = ContextEvidenceChecker.CheckEvidence(
                answerString,
                preparedContext.ContextStrategy.ProjectSections,
                comparisonInputs.ExpectedIdStrings);
            ContextResult contextResult = CreateResult(
                preparedContext,
                answerString,
                agentResponse.Usage,
                stopwatch.ElapsedMilliseconds,
                comparisonInputs.ExpectedIdStrings,
                citedIdStrings,
                evidenceFlagStrings);
            contextResults.Add(contextResult);
        }

        return new(
            QuestionString: comparisonInputs.QuestionString,
            SourceFileString: sourceFileString,
            ModelString: providerSettings.ModelString,
            TemperatureFloat: TemperatureFloat,
            MaxOutputTokensInt: MaxOutputTokensInt,
            ContextCharacterCapInt: ContextCreatorHelpers.ContextCharacterCapInt,
            PromptCharacterCapInt: ContextCreatorHelpers.PromptCharacterCapInt,
            FullStrategyString: FullContextStrategy.StrategyDescriptionString,
            SelectedStrategyString: SelectedContextStrategy.StrategyDescriptionString,
            TokenEstimateMethodString:
                "Estimate = ceiling(system and user prompt characters / 4). " +
                "Excludes message framing; not a model tokenizer. Provider usage is null when unavailable.",
            ContextResults: contextResults);
    }

    // Send only this context so the model cannot use an earlier answer.
    private static async Task<AgentResponse> RequestAsync(
        ProviderSettings providerSettings,
        PreparedContext preparedContext,
        HttpClient? httpClient,
        CancellationToken cancellationToken)
    {
        var aiClientFactory = new AiClientFactory();
        using IChatClient iChatClient = aiClientFactory.CreateClient(
            providerSettings,
            httpClient);
        ChatClientAgent chatClientAgent = new(
            iChatClient,
            new ChatClientAgentOptions
            {
                Name = "ResumeContext",
                ChatOptions = new ChatOptions
                {
                    Instructions = ContextCreatorHelpers.SystemInstructionsString,
                    Temperature = TemperatureFloat,
                    MaxOutputTokens = MaxOutputTokensInt
                }
            });

        cancellationToken.ThrowIfCancellationRequested();
        using var deadlineCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadlineCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(AiClientFactory.RequestTimeoutSeconds));

        AgentResponse agentResponse = await chatClientAgent.RunAsync(
            preparedContext.PromptString,
            cancellationToken: deadlineCancellationTokenSource.Token);

        // Preserve both comparison results only when each response is present and complete.
        if (agentResponse.FinishReason == ChatFinishReason.Length)
        {
            throw new InvalidDataException("The provider truncated its response at the output token limit.");
        }

        string? answerString = agentResponse.Messages.LastOrDefault(chatMessage =>
            chatMessage.Role == ChatRole.Assistant)?.Text;
        if (string.IsNullOrWhiteSpace(answerString))
        {
            throw new InvalidDataException(
                "The provider returned no response text. Check the model and its reasoning/output token budget.");
        }

        return agentResponse;
    }

    // Keep failed evidence checks with the answer so both results can still be compared.
    private static ContextResult CreateResult(
        PreparedContext preparedContext,
        string responseTextString,
        UsageDetails? usageDetails,
        long latencyMillisecondsLong,
        List<string>? expectedIdStrings,
        List<string> citedIdStrings,
        List<string> evidenceFlagStrings)
    {
        ContextStrategy contextStrategy = preparedContext.ContextStrategy;
        string answerString = responseTextString;

        // Missing measurements mean unknown, even when the framework returns an empty usage object.
        TokenUsage? providerTokenUsage = usageDetails is null ||
            (usageDetails.InputTokenCount is null && usageDetails.OutputTokenCount is null
            && usageDetails.TotalTokenCount is null)
            ? null : new(
                usageDetails.InputTokenCount,
                usageDetails.OutputTokenCount,
                usageDetails.TotalTokenCount);

        List<string> includedIdStrings = contextStrategy.ProjectSections.Select(projectSection =>
            projectSection.IdString).ToList();
        int promptCharactersInt =
            ContextCreatorHelpers.SystemInstructionsString.Length + preparedContext.PromptString.Length;

        return new(
            StrategyString: contextStrategy.StrategyString,
            IncludedSectionIdStrings: includedIdStrings,
            ContextCharactersInt: contextStrategy.TextString.Length,
            PromptCharactersInt: promptCharactersInt,
            EstimatedPromptTokensInt: (int)Math.Ceiling(promptCharactersInt / 4.0),
            ProviderTokenUsage: providerTokenUsage,
            LatencyMillisecondsLong: latencyMillisecondsLong,
            SourceRecallDouble: CalculateSourceRecall(
                includedIdStrings,
                expectedIdStrings),
            ResponseString: answerString,
            CitedSectionIdStrings: citedIdStrings,
            EvidenceFlagStrings: evidenceFlagStrings);
    }

    // Source recall is the share of expected project IDs included in the context; it does not rate the answer.
    private static double? CalculateSourceRecall(
        List<string> includedIdStrings,
        List<string>? expectedIdStrings)
    {
        return expectedIdStrings is { Count: > 0 }
            ? (double)expectedIdStrings.Count(idString => includedIdStrings.Contains(
                idString,
                StringComparer.Ordinal)) / expectedIdStrings.Count
            : null;
    }
}
