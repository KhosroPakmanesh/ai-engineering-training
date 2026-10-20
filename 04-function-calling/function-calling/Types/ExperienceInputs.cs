// Keep the original dates so tool arguments can be checked against the supplied jobs.
public sealed record ExperienceInputs(string Question, List<WorkInterval> Intervals, string? EvaluationDate);
