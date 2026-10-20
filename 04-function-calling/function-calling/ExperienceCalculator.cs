using System.Globalization;

public static class ExperienceCalculator
{
    // Count covered calendar months after merging jobs, so concurrent work counts only once.
    public static ExperienceResult CalculateExperience(List<WorkInterval> intervals, string? evaluationDate)
    {
        List<MergedWorkInterval> sortedIntervals = intervals.Select(interval => new MergedWorkInterval(
            ReadDate(interval.StartDate, "startDate"), ReadDate(interval.EndDate ?? evaluationDate, "endDate")))
            .OrderBy(interval => interval.StartDate).ThenBy(interval => interval.EndDate).ToList();
        List<MergedWorkInterval> mergedIntervals = MergeIntervals(sortedIntervals);

        int totalMonths = 0;
        foreach (MergedWorkInterval interval in mergedIntervals)
        {
            // A partial first month is excluded. The exclusive end must reach the next month's first day.
            int firstMonth = interval.StartDate.Year * 12 + interval.StartDate.Month - 1;
            if (interval.StartDate.Day != 1) firstMonth++;
            int endMonth = interval.EndDate.Year * 12 + interval.EndDate.Month - 1;
            totalMonths += Math.Max(0, endMonth - firstMonth);
        }

        return new(TotalMonths: totalMonths, InputIntervalCount: intervals.Count, MergedIntervals: mergedIntervals);
    }

    // Joining touching jobs also preserves a full month split between two consecutive roles.
    public static List<MergedWorkInterval> MergeIntervals(List<MergedWorkInterval> sortedIntervals)
    {
        var mergedIntervals = new List<MergedWorkInterval>();
        MergedWorkInterval current = sortedIntervals[0];
        foreach (MergedWorkInterval next in sortedIntervals.Skip(1))
        {
            if (next.StartDate <= current.EndDate)
            {
                if (next.EndDate > current.EndDate) current = current with { EndDate = next.EndDate };
            }
            else
            {
                mergedIntervals.Add(current);
                current = next;
            }
        }
        mergedIntervals.Add(current);
        return mergedIntervals;
    }

    // Exact ISO dates avoid inventing a day from a year, a month, or an ambiguous date.
    public static DateOnly ReadDate(string? date, string fieldName)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out DateOnly parsedDate))
            throw new InvalidDataException($"{fieldName} must be a valid yyyy-MM-dd date.");
        return parsedDate;
    }
}
