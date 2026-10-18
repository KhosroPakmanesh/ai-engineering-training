public record QuestionFixture(string Question, List<string> ExpectedSectionIds);
public record TokenUsage(long? InputTokens, long? OutputTokens, long? TotalTokens);
public record ContextResult(string Strategy, List<string> IncludedSectionIds, int ContextCharacters,
    int PromptCharacters, int EstimatedPromptTokens, TokenUsage? ProviderUsage,
    long LatencyMilliseconds, double? SourceRecall, string Response, List<string> CitedSectionIds, List<string> EvidenceFlags);
public record ComparisonReport(string Question, string SourceFile, string Model, float Temperature,
    int MaxOutputTokens, int ContextCharacterCap, int PromptCharacterCap, string FullStrategy,
    string SelectedStrategy, string TokenEstimateMethod, List<ContextResult> Results);
