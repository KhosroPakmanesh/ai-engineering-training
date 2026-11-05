using System.Text.Json.Serialization;

// A strategy contains whole source sections and their unchanged combined text.
public record ContextStrategy(
    [property: JsonPropertyName("strategy")] string StrategyString,
    [property: JsonPropertyName("sections")] List<ProjectSection> ProjectSections,
    [property: JsonPropertyName("text")] string TextString);
