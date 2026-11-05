using System.Text.Json.Serialization;

// Keep the original question, jobs, and explicit evaluation date together.
public sealed record ExperienceInputs(
    [property: JsonPropertyName("question")] string QuestionString,
    [property: JsonPropertyName("intervals")] List<WorkInterval> WorkIntervals,
    [property: JsonPropertyName("evaluationDate")] string? EvaluationDateString);
