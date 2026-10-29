using System.Text.Json.Serialization;

namespace ResumeRag;

// Keep the original JSON field names while code members show their full data types.
public sealed record DocumentChunk(
    [property: JsonPropertyName("ChunkId")] string ChunkIdString,
    [property: JsonPropertyName("Filename")] string FilenameString,
    [property: JsonPropertyName("Heading")] string HeadingString,
    [property: JsonPropertyName("Text")] string TextString,
    [property: JsonPropertyName("DocumentHash")] string DocumentHashString);
