using Xunit;

public class ExperienceCalculatorTests
{
    // Separate jobs sum their covered full months; the source list must remain untouched.
    [Fact]
    public void Nonoverlapping_jobs_count_complete_calendar_months_without_changing_inputs()
    {
        List<WorkInterval> intervals = [new("2022-04-01", "2022-07-01"), new("2020-01-01", "2020-03-01")];
        ExperienceInputsValidator.ValidateIntervals(intervals, null);
        ExperienceResult result = ExperienceCalculator.CalculateExperience(intervals, null);
        Assert.Equal(5, result.TotalMonths);
        Assert.Equal(2, result.MergedIntervals.Count);
        Assert.Equal("2022-04-01", intervals[0].StartDate);
    }

    // The first two copied jobs overlap for six months; a current role uses the explicit reference date.
    [Fact]
    public async Task Sample_jobs_merge_overlaps_and_resolve_current_roles()
    {
        ExperienceInputs inputs = await ExperienceTestData.ReadInputsAsync();
        ExperienceInputsValidator.ValidateInputs(inputs);
        ExperienceResult result = ExperienceCalculator.CalculateExperience(inputs.Intervals, inputs.EvaluationDate);
        Assert.Equal(36, result.TotalMonths);
        Assert.Equal(3, result.InputIntervalCount);
        Assert.Equal(new MergedWorkInterval(new(2020, 1, 1), new(2022, 1, 1)), result.MergedIntervals[0]);
        Assert.Equal(new MergedWorkInterval(new(2022, 4, 1), new(2023, 4, 1)), result.MergedIntervals[1]);
        Assert.Null(inputs.Intervals[2].EndDate);
    }

    // Only entire named calendar months count, including leap February; end dates are exclusive.
    [Theory]
    [InlineData("2024-01-15", "2024-03-15", 1)]
    [InlineData("2024-02-01", "2024-03-01", 1)]
    [InlineData("2024-02-01", "2024-02-29", 0)]
    [InlineData("2024-03-01", "2024-03-01", 0)]
    [InlineData("9999-12-31", "9999-12-31", 0)]
    public void Partial_months_and_date_boundaries_follow_the_calendar_rule(string startDate, string endDate, int expectedMonths)
    {
        List<WorkInterval> intervals = [new(startDate, endDate)];
        ExperienceInputsValidator.ValidateIntervals(intervals, null);
        ExperienceResult result = ExperienceCalculator.CalculateExperience(intervals, null);
        Assert.Equal(expectedMonths, result.TotalMonths);
    }

    // Nested, duplicate, and touching intervals must not lose or double-count a month.
    [Fact]
    public void Contiguous_jobs_form_a_complete_month_before_counting()
    {
        List<WorkInterval> intervals =
        [
            new("2024-01-15", "2024-02-01"), new("2024-01-01", "2024-01-15"),
            new("2024-01-20", "2024-01-25"), new("2024-01-01", "2024-01-15")
        ];
        ExperienceInputsValidator.ValidateIntervals(intervals, null);
        ExperienceResult result = ExperienceCalculator.CalculateExperience(intervals, null);
        Assert.Equal(1, result.TotalMonths);
        Assert.Single(result.MergedIntervals);
    }
}
