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
}
