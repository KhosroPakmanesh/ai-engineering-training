public static class ExperienceInputsValidator
{
    // Find missing dates before a paid request; never substitute the computer's current date.
    public static void ValidateInputs(ExperienceInputs inputs)
    {
        if (string.IsNullOrWhiteSpace(inputs.Question) || inputs.Question.Length > 500)
            throw new InvalidDataException("The question must contain between 1 and 500 characters.");

        ValidateIntervals(inputs.Intervals, inputs.EvaluationDate);
    }

    // Validation reports invalid input without sorting, changing dates, or filling in unknown values.
    public static void ValidateIntervals(List<WorkInterval> intervals, string? evaluationDate)
    {
        if (intervals is null || intervals.Count == 0)
            throw new InvalidDataException("Supply at least one work interval.");
        if (evaluationDate is not null) ExperienceCalculator.ReadDate(evaluationDate, "evaluationDate");

        foreach (WorkInterval interval in intervals)
        {
            if (interval is null) throw new InvalidDataException("A work interval cannot be null.");
            DateOnly startDate = ExperienceCalculator.ReadDate(interval.StartDate, "startDate");
            if (interval.EndDate is null && evaluationDate is null)
                throw new InvalidDataException("A current role requires an explicit evaluationDate.");
            DateOnly endDate = ExperienceCalculator.ReadDate(interval.EndDate ?? evaluationDate, "endDate");
            if (endDate < startDate)
                throw new InvalidDataException("A work interval's endDate precedes its startDate.");
        }
    }
}
