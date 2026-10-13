// Fixtures identify expected sources for measurement; they are never supplied as answers to the model.
public record QuestionFixture(string Question, string[] ExpectedSectionIds);

// Provider usage is optional because some responses omit token counts.
public record TokenUsage(long? InputTokens, long? OutputTokens, long? TotalTokens);

// One strategy's answer travels with its input measurements and evidence flags.
public record ContextResult(string Strategy, string[] IncludedSectionIds, int ContextCharacters,
    int PromptCharacters, int EstimatedPromptTokens, TokenUsage? ProviderUsage,
    long LatencyMilliseconds, double? SourceRecall, string Response, string[] CitedSectionIds, string[] EvidenceFlags);

// Preserve the full experiment settings beside both results in the exported JSON.
public record ComparisonReport(string Question, string SourceFile, string Model, float Temperature,
    int MaxOutputTokens, int ContextCharacterCap, int PromptCharacterCap, string FullStrategy,
    string SelectedStrategy, string TokenEstimateMethod, ContextResult[] Results);
