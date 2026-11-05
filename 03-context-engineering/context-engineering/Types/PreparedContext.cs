using System.Text.Json.Serialization;

// Pair a selected context with the complete question prompt prepared for the request.
public sealed record PreparedContext(
    [property: JsonPropertyName("context")] ContextStrategy ContextStrategy,
    [property: JsonPropertyName("prompt")] string PromptString);
