using System.Text.Json;
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

    // A different or repeated function is not evidence for this lesson's single calculation.
    [Fact]
    public void Wrong_or_multiple_tool_records_are_rejected()
    {
        AiToolExecution execution = new("OtherTool", JsonSerializer.SerializeToElement(new { totalMonths = 36 }), 0);
        AiResponse response = new("answer", null, 0) { ToolExecutions = [execution] };
        Assert.Throws<InvalidDataException>(() => ToolExecutionValidator.ValidateToolExecution(response));
        response = response with { ToolExecutions = [execution with { ToolName = "CalculateExperience" }, execution] };
        Assert.Throws<InvalidDataException>(() => ToolExecutionValidator.ValidateToolExecution(response));
    }
}
