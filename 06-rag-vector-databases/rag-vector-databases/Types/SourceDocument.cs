using System.Text.Json.Serialization;

namespace ResumeRag;

// Keep the original JSON field names while code members show their full data types.
public sealed record SourceDocument(
    [property: JsonPropertyName("Filename")] string FilenameString,
    [property: JsonPropertyName("Text")] string TextString);
