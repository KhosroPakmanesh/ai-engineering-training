using System.Text.Json.Serialization;

namespace ResumeRag;

// Keep the original JSON field names while code members show their full data types.
public sealed record ChunkIndex(
    [property: JsonPropertyName("Metadata")] IndexMetadata IndexMetadata,
    [property: JsonPropertyName("Chunks")] List<IndexedChunk> IndexedChunks);
