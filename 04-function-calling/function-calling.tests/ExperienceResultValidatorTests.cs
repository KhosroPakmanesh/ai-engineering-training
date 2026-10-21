using Xunit;

public class ExperienceResultValidatorTests
{
    // Check both the computed total and its citation; extra claims cannot pass this small answer contract.
    [Theory]
    [InlineData("36 full calendar months [CalculateExperience.totalMonths].", true)]
    [InlineData("42 full calendar months [CalculateExperience.totalMonths].", false)]
    [InlineData("36 full calendar months.", false)]
    [InlineData("36 full calendar months [CalculateExperience.totalMonths]. Another 5 years.", false)]
    public void Final_answers_must_match_the_executed_result(string answer, bool expectedValid)
    {
        ExperienceResult result = new(36, 3, []);
        if (expectedValid) ExperienceResultValidator.ValidateAnswer(answer, result);
        else Assert.Throws<InvalidDataException>(() => ExperienceResultValidator.ValidateAnswer(answer, result));
    }
}
