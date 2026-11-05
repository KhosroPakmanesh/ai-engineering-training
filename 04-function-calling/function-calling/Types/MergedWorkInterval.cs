using System.Text.Json.Serialization;

// A merged interval represents covered dates without counting overlapping jobs twice.
public sealed record MergedWorkInterval(
    [property: JsonPropertyName("startDate")] DateOnly StartDateOnly,
    [property: JsonPropertyName("endDate")] DateOnly EndDateOnly);
