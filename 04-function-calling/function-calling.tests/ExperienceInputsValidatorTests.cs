using Xunit;

public class ExperienceInputsValidatorTests
{
    // Invalid questions and empty source collections are caught before a model request.
    [Fact]
    public void Preparation_rejects_empty_questions_and_missing_jobs()
    {
        Assert.Throws<InvalidDataException>(() => ExperienceInputsValidator.ValidateInputs(new(" ", [new("2024-01-01", "2025-01-01")], null)));
        Assert.Throws<InvalidDataException>(() => ExperienceInputsValidator.ValidateInputs(new("How much experience?", [], null)));
    }

    // Reject unusable dates before calculation, including a missing or invalid reference for current roles.
    [Theory]
    [InlineData("2024-02-30", "2024-03-01", null)]
    [InlineData("2024-03-01", "2024-02-01", null)]
    [InlineData("2024", "2025-01-01", null)]
    [InlineData("2024-01-01", null, null)]
    [InlineData("2024-01-01", null, "not-a-date")]
    [InlineData("2024-01-01", null, "2023-01-01")]
    public void Invalid_or_unresolved_intervals_are_rejected(string startDate, string? endDate, string? evaluationDate)
    {
        Assert.Throws<InvalidDataException>(() =>
            ExperienceInputsValidator.ValidateIntervals([new(startDate, endDate)], evaluationDate));
    }
}
