using Xunit;

public class ExperienceCalculatorTests
{
    // Separate jobs sum their covered full months; the source list must remain untouched.
    [Fact]
    public void Nonoverlapping_jobs_count_complete_calendar_months_without_changing_inputs()
    {
        // Arrange
        List<WorkInterval> workIntervals =
        [
            new(
            "2022-04-01",
            "2022-07-01"),
            new(
                "2020-01-01",
                "2020-03-01")
        ];
        ExperienceInputsValidator.ValidateIntervals(
            workIntervals,
            null);

        // Act
        ExperienceResult experienceResult = ExperienceCalculator.CalculateExperience(
            workIntervals,
            null);

        // Assert
        Assert.Equal(
            5,
            experienceResult.TotalMonthsInt);
        Assert.Equal(
            2,
            experienceResult.MergedWorkIntervals.Count);
        Assert.Equal(
            "2022-04-01",
            workIntervals[0].StartDateString);
    }

    // The first two copied jobs overlap for six months; a current role uses the explicit reference date.
    [Fact]
    public async Task Sample_jobs_merge_overlaps_and_resolve_current_roles()
    {
        // Arrange
        ExperienceInputs experienceInputs = await ExperienceTestData.ReadInputsAsync();
        ExperienceInputsValidator.ValidateInputs(experienceInputs);

        // Act
        ExperienceResult experienceResult = ExperienceCalculator.CalculateExperience(
            experienceInputs.WorkIntervals,
            experienceInputs.EvaluationDateString);

        // Assert
        Assert.Equal(
            36,
            experienceResult.TotalMonthsInt);
        Assert.Equal(
            3,
            experienceResult.InputIntervalCount);
        Assert.Equal(
            new MergedWorkInterval(
                new(
                    2020,
                    1,
                    1),
                new(
                    2022,
                    1,
                    1)),
            experienceResult.MergedWorkIntervals[0]);
        Assert.Equal(
            new MergedWorkInterval(
                new(
                    2022,
                    4,
                    1),
                new(
                    2023,
                    4,
                    1)),
            experienceResult.MergedWorkIntervals[1]);
        Assert.Null(experienceInputs.WorkIntervals[2].EndDateString);
    }

    // Only entire named calendar months count, including leap February; end dates are exclusive.
    [Theory]
    [InlineData(
        "2024-01-15",
        "2024-03-15",
        1)]
    [InlineData(
        "2024-02-01",
        "2024-03-01",
        1)]
    [InlineData(
        "2024-02-01",
        "2024-02-29",
        0)]
    [InlineData(
        "2024-03-01",
        "2024-03-01",
        0)]
    [InlineData(
        "9999-12-31",
        "9999-12-31",
        0)]
    public void Partial_months_and_date_boundaries_follow_the_calendar_rule(
        string startDateString,
        string endDateString,
        int expectedMonthsInt)
    {
        // Arrange
        List<WorkInterval> workIntervals =
        [
            new(
            startDateString,
            endDateString)
        ];
        ExperienceInputsValidator.ValidateIntervals(
            workIntervals,
            null);

        // Act
        ExperienceResult experienceResult = ExperienceCalculator.CalculateExperience(
            workIntervals,
            null);

        // Assert
        Assert.Equal(
            expectedMonthsInt,
            experienceResult.TotalMonthsInt);
    }

    // Nested, duplicate, and touching intervals must not lose or double-count a month.
    [Fact]
    public void Contiguous_jobs_form_a_complete_month_before_counting()
    {
        // Arrange
        List<WorkInterval> workIntervals =
        [
            new(
                "2024-01-15",
                "2024-02-01"),
            new(
                    "2024-01-01",
                    "2024-01-15"),
            new(
                "2024-01-20",
                "2024-01-25"),
            new(
                    "2024-01-01",
                    "2024-01-15")
        ];
        ExperienceInputsValidator.ValidateIntervals(
            workIntervals,
            null);

        // Act
        ExperienceResult experienceResult = ExperienceCalculator.CalculateExperience(
            workIntervals,
            null);

        // Assert
        Assert.Equal(
            1,
            experienceResult.TotalMonthsInt);
        Assert.Single(experienceResult.MergedWorkIntervals);
    }
}
