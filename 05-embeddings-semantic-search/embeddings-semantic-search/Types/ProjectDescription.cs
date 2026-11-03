using System.Text.Json.Serialization;

// Both searches compare the original text; skills remain fictional metadata.
public sealed record ProjectDescription([property: JsonPropertyName("id")] string IdString,
    [property: JsonPropertyName("text")] string TextString, [property: JsonPropertyName("skills")] List<string> SkillStrings);
