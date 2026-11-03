using System.Text.Json.Serialization;

namespace ResumeRag;
// A bounded source passage keeps its heading, citation ID, and document hash.
public sealed record DocumentChunk([property: JsonPropertyName("ChunkId")] string ChunkIdString,
    [property: JsonPropertyName("Filename")] string FilenameString, [property: JsonPropertyName("Heading")] string HeadingString,
    [property: JsonPropertyName("Text")] string TextString, [property: JsonPropertyName("DocumentHash")] string DocumentHashString);
