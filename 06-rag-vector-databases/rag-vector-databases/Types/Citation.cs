using System.Text.Json.Serialization;

namespace ResumeRag;

// Keep the original JSON field names while code members show their full data types.
public sealed record Citation(
    [property: JsonPropertyName("chunkId")] string ChunkIdString,
    [property: JsonPropertyName("source")] string SourceString);
