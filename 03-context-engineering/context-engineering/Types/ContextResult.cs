using System.Text.Json.Serialization;

// Keep one answer beside its supplied sources, measurements, and evidence flags.
public record ContextResult(
    [property: JsonPropertyName("strategy")] string StrategyString,
    [property: JsonPropertyName("includedSectionIds")] List<string> IncludedSectionIdStrings,
    [property: JsonPropertyName("contextCharacters")] int ContextCharactersInt,
    [property: JsonPropertyName("promptCharacters")] int PromptCharactersInt,
    [property: JsonPropertyName("estimatedPromptTokens")] int EstimatedPromptTokensInt,
    [property: JsonPropertyName("providerUsage")] TokenUsage? ProviderTokenUsage,
    [property: JsonPropertyName("latencyMilliseconds")] long LatencyMillisecondsLong,
    [property: JsonPropertyName("sourceRecall")] double? SourceRecallDouble,
    [property: JsonPropertyName("response")] string ResponseString,
    [property: JsonPropertyName("citedSectionIds")] List<string> CitedSectionIdStrings,
    [property: JsonPropertyName("evidenceFlags")] List<string> EvidenceFlagStrings);
