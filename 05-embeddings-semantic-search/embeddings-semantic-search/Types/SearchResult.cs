using System.Text.Json.Serialization;

// One ranked match contains its position, source ID, score, and short excerpt.
public sealed record SearchResult([property: JsonPropertyName("rank")] int RankInt,
    [property: JsonPropertyName("documentId")] string DocumentIdString, [property: JsonPropertyName("score")] double ScoreDouble,
    [property: JsonPropertyName("snippet")] string SnippetString);
