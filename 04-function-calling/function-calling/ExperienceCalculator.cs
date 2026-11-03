using System.Globalization;

public static class ExperienceCalculator
{
    // Count covered calendar months after merging jobs, so concurrent work counts only once.
    public static ExperienceResult CalculateExperience(List<WorkInterval> workIntervals, string? evaluationDateString)
    {
        List<MergedWorkInterval> sortedMergedWorkIntervals = workIntervals.Select(workInterval => new MergedWorkInterval(
            ReadDate(workInterval.StartDateString, "startDate"), ReadDate(workInterval.EndDateString ?? evaluationDateString, "endDate")))
            .OrderBy(mergedWorkInterval => mergedWorkInterval.StartDateOnly).ThenBy(mergedWorkInterval => mergedWorkInterval.EndDateOnly).ToList();
        List<MergedWorkInterval> mergedWorkIntervals = MergeIntervals(sortedMergedWorkIntervals);

        int totalMonthsInt = 0;
        foreach (MergedWorkInterval mergedWorkInterval in mergedWorkIntervals)
        {
            // A partial first month is excluded. The exclusive end must reach the next month's first day.
            int firstMonthInt = mergedWorkInterval.StartDateOnly.Year * 12 + mergedWorkInterval.StartDateOnly.Month - 1;
            if (mergedWorkInterval.StartDateOnly.Day != 1)
            {
                firstMonthInt++;
            }

            int endMonthInt = mergedWorkInterval.EndDateOnly.Year * 12 + mergedWorkInterval.EndDateOnly.Month - 1;
            totalMonthsInt += Math.Max(0, endMonthInt - firstMonthInt);
        }

        return new(TotalMonthsInt: totalMonthsInt, InputIntervalCount: workIntervals.Count, MergedWorkIntervals: mergedWorkIntervals);
    }

    // Joining touching jobs also preserves a full month split between two consecutive roles.
    public static List<MergedWorkInterval> MergeIntervals(List<MergedWorkInterval> sortedMergedWorkIntervals)
    {
        var mergedWorkIntervals = new List<MergedWorkInterval>();
        MergedWorkInterval currentMergedWorkInterval = sortedMergedWorkIntervals[0];
        foreach (MergedWorkInterval nextMergedWorkInterval in sortedMergedWorkIntervals.Skip(1))
        {
            if (nextMergedWorkInterval.StartDateOnly <= currentMergedWorkInterval.EndDateOnly)
            {
                if (nextMergedWorkInterval.EndDateOnly > currentMergedWorkInterval.EndDateOnly)
                {
                    currentMergedWorkInterval = currentMergedWorkInterval with
                    {
                        EndDateOnly = nextMergedWorkInterval.EndDateOnly
                    };
                }
            }
            else
            {
                mergedWorkIntervals.Add(currentMergedWorkInterval);
                currentMergedWorkInterval = nextMergedWorkInterval;
            }
        }
        mergedWorkIntervals.Add(currentMergedWorkInterval);
        return mergedWorkIntervals;
    }

    // Exact ISO dates avoid inventing a day from a year, a month, or an ambiguous date.
    public static DateOnly ReadDate(string? dateString, string fieldNameString)
    {
        if (!DateOnly.TryParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
            out DateOnly parsedDateOnly))
        {
            throw new InvalidDataException($"{fieldNameString} must be a valid yyyy-MM-dd date.");
        }

        return parsedDateOnly;
    }
}
