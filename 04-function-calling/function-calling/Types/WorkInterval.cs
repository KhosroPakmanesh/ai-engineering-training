using System.Text.Json.Serialization;

// A source job has complete ISO dates; a missing end date means the current role.
public sealed record WorkInterval(
    [property: JsonPropertyName("startDate")] string StartDateString,
    [property: JsonPropertyName("endDate")] string? EndDateString);
