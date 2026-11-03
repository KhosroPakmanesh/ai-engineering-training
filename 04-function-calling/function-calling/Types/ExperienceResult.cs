using System.Text.Json.Serialization;

// The calculator returns full months, input count, and merged coverage intervals.
public sealed record ExperienceResult([property: JsonPropertyName("totalMonths")] int TotalMonthsInt, int InputIntervalCount,
    [property: JsonPropertyName("mergedIntervals")] List<MergedWorkInterval> MergedWorkIntervals);
