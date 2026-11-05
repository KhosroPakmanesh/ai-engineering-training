using System.Text.Json.Serialization;

namespace ResumeRag;
// A retrieved source passage carries its cosine similarity score.
public sealed record RetrievedChunk(
    [property: JsonPropertyName("Chunk")] DocumentChunk DocumentChunk,
    [property: JsonPropertyName("Score")] double ScoreDouble);
