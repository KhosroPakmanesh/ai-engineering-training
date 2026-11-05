using System.Text.Json.Serialization;

// A stated skill and optional proficiency retain their supporting quote.
public sealed record Skill(
    [property: JsonPropertyName("name")] string NameString,
    [property: JsonPropertyName("proficiency")] string? ProficiencyString,
    Evidence Evidence);
