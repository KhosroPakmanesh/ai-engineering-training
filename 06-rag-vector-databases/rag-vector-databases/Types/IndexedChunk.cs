using System.Text.Json.Serialization;

namespace ResumeRag;
// A source passage is paired with its normalized stored vector.
public sealed record IndexedChunk([property: JsonPropertyName("Chunk")] DocumentChunk DocumentChunk,
    [property: JsonPropertyName("Vector")] List<float> VectorFloats);
