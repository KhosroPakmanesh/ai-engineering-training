using System.Text.Json.Serialization;

namespace ResumeRag;

// Keep the original JSON field names while code members show their full data types.
public sealed record IndexedChunk(
    [property: JsonPropertyName("Chunk")] DocumentChunk DocumentChunk,
    [property: JsonPropertyName("Vector")] List<float> VectorFloats);
