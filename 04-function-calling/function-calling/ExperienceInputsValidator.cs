public static class ExperienceInputsValidator
{
    // Find missing dates before a paid request; never substitute the computer's current date.
    public static void ValidateInputs(ExperienceInputs experienceInputs)
    {
        if (string.IsNullOrWhiteSpace(experienceInputs.QuestionString) || experienceInputs.QuestionString.Length > 500)
        {
            throw new InvalidDataException("The question must contain between 1 and 500 characters.");
        }

        ValidateIntervals(
            experienceInputs.WorkIntervals,
            experienceInputs.EvaluationDateString);
    }

    // Validation reports invalid input without sorting, changing dates, or filling in unknown values.
    public static void ValidateIntervals(
        List<WorkInterval> workIntervals,
        string? evaluationDateString)
    {
        if (workIntervals is null || workIntervals.Count == 0)
        {
            throw new InvalidDataException("Supply at least one work interval.");
        }

        if (evaluationDateString is not null)
        {
            ExperienceCalculator.ReadDate(
                evaluationDateString,
                "evaluationDate");
        }

        foreach (WorkInterval workInterval in workIntervals)
        {
            if (workInterval is null)
            {
                throw new InvalidDataException("A work interval cannot be null.");
            }

            DateOnly startDateOnly = ExperienceCalculator.ReadDate(
                workInterval.StartDateString,
                "startDate");
            if (workInterval.EndDateString is null && evaluationDateString is null)
            {
                throw new InvalidDataException("A current role requires an explicit evaluationDate.");
            }

            DateOnly endDateOnly = ExperienceCalculator.ReadDate(
                workInterval.EndDateString ?? evaluationDateString,
                "endDate");
            if (endDateOnly < startDateOnly)
            {
                throw new InvalidDataException("A work interval's endDate precedes its startDate.");
            }
        }
    }
}
