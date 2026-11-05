using System.Text.Json.Serialization;

namespace ResumeRag;
// Save each candidate's ID and score, including passages excluded from context.
public sealed record RetrievalScore(
    [property: JsonPropertyName("chunkId")] string ChunkIdString,
    [property: JsonPropertyName("score")] double ScoreDouble);
