using System.Text.Json.Serialization;

namespace ResumeRag;
// A complete stored snapshot contains metadata and its current indexed passages.
public sealed record ChunkIndex(
    [property: JsonPropertyName("Metadata")] IndexMetadata IndexMetadata,
    [property: JsonPropertyName("Chunks")] List<IndexedChunk> IndexedChunks);
