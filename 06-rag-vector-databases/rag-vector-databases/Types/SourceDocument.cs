using System.Text.Json.Serialization;

namespace ResumeRag;
// An original Markdown filename and its text enter preparation together.
public sealed record SourceDocument([property: JsonPropertyName("Filename")] string FilenameString,
    [property: JsonPropertyName("Text")] string TextString);
