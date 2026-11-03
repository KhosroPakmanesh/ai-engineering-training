using System.Text.Json.Serialization;

// One education entry keeps unknown qualifications and dates as null.
public sealed record Education([property: JsonPropertyName("institution")] string? InstitutionString,
    [property: JsonPropertyName("degree")] string? DegreeString, [property: JsonPropertyName("fieldOfStudy")] string? FieldOfStudyString,
    [property: JsonPropertyName("startDate")] string? StartDateString, [property: JsonPropertyName("endDate")] string? EndDateString,
    Evidence Evidence);
