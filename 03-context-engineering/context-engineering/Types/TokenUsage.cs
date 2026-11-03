using System.Text.Json.Serialization;

// Missing provider token counts stay unknown rather than becoming zero.
public record TokenUsage([property: JsonPropertyName("inputTokens")] long? InputTokensLong,
    [property: JsonPropertyName("outputTokens")] long? OutputTokensLong, [property: JsonPropertyName("totalTokens")] long? TotalTokensLong);
