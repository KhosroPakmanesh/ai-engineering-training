using System.Text.Json.Serialization;

namespace ResumeRag;

// Keep the original JSON field names while code members show their full data types.
public sealed record RetrievalScore(
    [property: JsonPropertyName("chunkId")] string ChunkIdString,
    [property: JsonPropertyName("score")] double ScoreDouble);
