using System.Text.Json.Serialization;

namespace ResumeRag;
// An accepted passage ID points back to its source filename.
public sealed record Citation([property: JsonPropertyName("chunkId")] string ChunkIdString,
    [property: JsonPropertyName("source")] string SourceString);
