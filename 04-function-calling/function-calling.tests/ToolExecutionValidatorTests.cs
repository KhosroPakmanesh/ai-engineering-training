using Xunit;

using System.Text.Json;

public class ToolExecutionValidatorTests
{
    // Even a correct-looking answer needs the execution record supplied by the real function loop.
    [Fact]
    public void An_answer_without_a_real_tool_execution_is_rejected()
    {
        AiResponse response = new("36 full calendar months [CalculateExperience.totalMonths].", null, 0);
        Assert.Throws<InvalidDataException>(() => ToolExecutionValidator.ValidateToolExecution(response));
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

