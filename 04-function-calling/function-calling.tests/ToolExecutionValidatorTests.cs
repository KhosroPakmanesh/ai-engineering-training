using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;

public class ToolExecutionValidatorTests
{
    // A requested function is insufficient without its actual successful result.
    [Fact]
    public void An_answer_without_a_real_tool_execution_is_rejected()
    {
        // Arrange
        AgentResponse agentResponse = new(
        [
            new ChatMessage(
                ChatRole.Assistant,
            [
                new FunctionCallContent(
                    "calculation-0",
                    "CalculateExperience",
                    new Dictionary<string, object?>())
            ]),
            new ChatMessage(
                ChatRole.Assistant,
                "36 full calendar months [CalculateExperience.totalMonths].")
        ]);

        // Act
        InvalidDataException errorInvalidDataException = Assert.Throws<InvalidDataException>(() =>
            ToolExecutionValidator.ValidateToolExecution(agentResponse));

        // Assert
        Assert.Contains(
            "did not execute",
            errorInvalidDataException.Message);
    }

    // Match each result to the calculator's name and require a successful execution.
    [Theory]
    [InlineData(
        "OtherTool",
        false)]
    [InlineData(
        "CalculateExperience",
        true)]
    public void Wrong_or_failed_function_results_are_rejected(
        string nameString,
        bool failedBool)
    {
        // Arrange
        FunctionResultContent resultFunctionResultContent = new(
            "calculation-0",
            new
            {
                totalMonths = 36
            });
        if (failedBool)
        {
            resultFunctionResultContent.Exception = new InvalidDataException("The source dates are invalid.");
        }

        AgentResponse agentResponse = new(
        [
            new ChatMessage(
                        ChatRole.Assistant,
            [
                new FunctionCallContent(
                            "calculation-0",
                            nameString,
                            new Dictionary<string, object?>())
            ]),
            new ChatMessage(
                ChatRole.Tool,
                [resultFunctionResultContent])
        ]);

        // Act
        InvalidDataException errorInvalidDataException = Assert.Throws<InvalidDataException>(() =>
            ToolExecutionValidator.ValidateToolExecution(agentResponse));

        // Assert
        Assert.Contains(
            "successful CalculateExperience",
            errorInvalidDataException.Message);
    }

    // A second successful execution violates the lesson's exact single-execution rule.
    [Fact]
    public void Multiple_calculator_results_are_rejected()
    {
        // Arrange
        AgentResponse agentResponse = new(
        [
            new ChatMessage(
                ChatRole.Assistant,
            [
                new FunctionCallContent(
                    "first",
                    "CalculateExperience",
                    new Dictionary<string, object?>()),
                new FunctionCallContent(
                    "second",
                    "CalculateExperience",
                    new Dictionary<string, object?>())
            ]),
            new ChatMessage(
                ChatRole.Tool,
            [
                new FunctionResultContent(
                    "first",
                    new
                {
                    totalMonths = 36
                }),
                new FunctionResultContent(
                    "second",
                    new
                {
                    totalMonths = 36
                })
            ])
        ]);

        // Act
        InvalidDataException errorInvalidDataException = Assert.Throws<InvalidDataException>(() =>
            ToolExecutionValidator.ValidateToolExecution(agentResponse));

        // Assert
        Assert.Contains(
            "exactly one CalculateExperience",
            errorInvalidDataException.Message);
    }
}
