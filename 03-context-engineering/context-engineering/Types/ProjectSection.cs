using System.Text.Json.Serialization;

// A source ID, heading, and original text identify one fictional project.
public record ProjectSection([property: JsonPropertyName("id")] string IdString,
    [property: JsonPropertyName("heading")] string HeadingString, [property: JsonPropertyName("text")] string TextString);
