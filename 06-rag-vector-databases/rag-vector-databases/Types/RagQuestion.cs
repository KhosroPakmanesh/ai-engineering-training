using System.Text.Json.Serialization;

namespace ResumeRag;

// Keep the original JSON field names while code members show their full data types.
public sealed record RagQuestion(
    [property: JsonPropertyName("Text")] string TextString,
    [property: JsonPropertyName("Skill")] string SkillString);
