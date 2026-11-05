using System.Text.Json.Serialization;

// The report records fixed settings and both independent context results.
public record ComparisonReport(
    [property: JsonPropertyName("question")] string QuestionString,
    [property: JsonPropertyName("sourceFile")] string SourceFileString,
    [property: JsonPropertyName("model")] string ModelString,
    [property: JsonPropertyName("temperature")] float TemperatureFloat,
    [property: JsonPropertyName("maxOutputTokens")] int MaxOutputTokensInt,
    [property: JsonPropertyName("contextCharacterCap")] int ContextCharacterCapInt,
    [property: JsonPropertyName("promptCharacterCap")] int PromptCharacterCapInt,
    [property: JsonPropertyName("fullStrategy")] string FullStrategyString,
    [property: JsonPropertyName("selectedStrategy")] string SelectedStrategyString,
    [property: JsonPropertyName("tokenEstimateMethod")] string TokenEstimateMethodString,
    [property: JsonPropertyName("results")] List<ContextResult> ContextResults);
