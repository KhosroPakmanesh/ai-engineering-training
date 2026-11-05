using System.Text.Json.Serialization;

// One job keeps its stated dates and duration beside the supporting quote.
public sealed record Experience(
    [property: JsonPropertyName("employer")] string? EmployerString,
    [property: JsonPropertyName("role")] string? RoleString,
    [property: JsonPropertyName("startDate")] string? StartDateString,
    [property: JsonPropertyName("endDate")] string? EndDateString,
    [property: JsonPropertyName("durationMonths")] int? DurationMonthsInt,
    Evidence Evidence);
