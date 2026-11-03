using System.Text.Json.Serialization;

// The original question, source sections, and expected IDs enter both context strategies together.
public sealed record ComparisonInputs([property: JsonPropertyName("question")] string QuestionString,
    [property: JsonPropertyName("sections")] List<ProjectSection> ProjectSections,
    [property: JsonPropertyName("expectedIds")] List<string>? ExpectedIdStrings);
