using Xunit;

public class ToolExecutionValidatorTests
{
    // Even a correct-looking answer needs the execution record supplied by the real function loop.
    [Fact]
    public void An_answer_without_a_real_tool_execution_is_rejected()
    {
        AiResponse response = new("36 full calendar months [CalculateExperience.totalMonths].", null, 0);
        Assert.Throws<InvalidDataException>(() => ToolExecutionValidator.ValidateToolExecution(response));
    }
}

