using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;

public class ToolExecutionValidatorTests
{
    // A requested function is insufficient without its actual successful result.
    [Fact]
    public void An_answer_without_a_real_tool_execution_is_rejected()
    {
        AgentResponse response = new(
        [
            new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("calculation-0", "CalculateExperience", new Dictionary<string, object?>())]),
            new ChatMessage(ChatRole.Assistant, "36 full calendar months [CalculateExperience.totalMonths].")
        ]);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => ToolExecutionValidator.ValidateToolExecution(response));
        Assert.Contains("did not execute", error.Message);
    }

    // Match each result to the calculator's name and require a successful execution.
    [Theory]
    [InlineData("OtherTool", false)]
    [InlineData("CalculateExperience", true)]
    public void Wrong_or_failed_function_results_are_rejected(string name, bool failed)
    {
        FunctionResultContent result = new("calculation-0", new { totalMonths = 36 });
        if (failed) result.Exception = new InvalidDataException("The source dates are invalid.");
        AgentResponse response = new(
        [
            new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("calculation-0", name, new Dictionary<string, object?>())]),
            new ChatMessage(ChatRole.Tool, [result])
        ]);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => ToolExecutionValidator.ValidateToolExecution(response));
        Assert.Contains("successful CalculateExperience", error.Message);
    }

    // A second successful execution violates the lesson's exact single-execution rule.
    [Fact]
    public void Multiple_calculator_results_are_rejected()
    {
        AgentResponse response = new(
        [
            new ChatMessage(ChatRole.Assistant,
            [
                new FunctionCallContent("first", "CalculateExperience", new Dictionary<string, object?>()),
                new FunctionCallContent("second", "CalculateExperience", new Dictionary<string, object?>())
            ]),
            new ChatMessage(ChatRole.Tool,
            [
                new FunctionResultContent("first", new { totalMonths = 36 }),
                new FunctionResultContent("second", new { totalMonths = 36 })
            ])
        ]);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => ToolExecutionValidator.ValidateToolExecution(response));
        Assert.Contains("exactly one CalculateExperience", error.Message);
    }
}
