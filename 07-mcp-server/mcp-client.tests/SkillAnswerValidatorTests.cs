using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;

namespace ResumeMcpClient.Tests;

public sealed class SkillAnswerValidatorTests
{
    // The tool result can be valid while the final text invents, omits, or fails to cite a skill.
    [Theory]
    [InlineData("Skills: C#, Rust [list_skills]")]
    [InlineData("Skills: C#")]
    public void Changed_answer_or_missing_citation_is_rejected(string answerString)
    {
        // Arrange
        AgentResponse agentResponse = CreateResponse(answerString);
        SkillAnswer skillAnswer = SkillAnswerParser.Parse(agentResponse);

        // Act
        InvalidDataException invalidDataException = Assert.Throws<InvalidDataException>(() =>
            SkillAnswerValidator.Validate(
                agentResponse,
                skillAnswer,
                ["C#"]));

        // Assert
        Assert.Contains(
            "source skills",
            invalidDataException.Message);
    }

    // A failed tool or an unrelated result ID cannot prove the requested skills were obtained.
    [Theory]
    [InlineData(
        true,
        "mcp-call")]
    [InlineData(
        false,
        "unrelated-call")]
    public void Failed_or_unmatched_tool_result_is_rejected(
        bool failedBool,
        string resultCallIdString)
    {
        // Arrange
        AgentResponse agentResponse = CreateResponse(
            "Skills: C# [list_skills]",
            failedBool,
            resultCallIdString);
        SkillAnswer skillAnswer = SkillAnswerParser.Parse(agentResponse);

        // Act
        InvalidDataException invalidDataException = Assert.Throws<InvalidDataException>(() =>
            SkillAnswerValidator.Validate(
                agentResponse,
                skillAnswer,
                ["C#"]));

        // Assert
        Assert.Contains(
            "successful list_skills",
            invalidDataException.Message);
    }

    // Keep all unrelated requirements valid so each rejection tests only its intended rule.
    private static AgentResponse CreateResponse(
        string answerString,
        bool failedBool = false,
        string resultCallIdString = "mcp-call")
    {
        var functionResultContent = new FunctionResultContent(
            resultCallIdString,
            new[] { "C#" });
        if (failedBool)
        {
            functionResultContent.Exception = new InvalidDataException("The MCP tool failed.");
        }

        return new(
        [
            new ChatMessage(
                ChatRole.Assistant,
            [
                new FunctionCallContent(
                    "mcp-call",
                    "list_skills",
                    new Dictionary<string, object?>
                {
                    ["profileIdString"] = "alex"
                })
            ]),
            new ChatMessage(
                ChatRole.Tool,
                [functionResultContent]),
            new ChatMessage(
                    ChatRole.Assistant,
                    answerString)
        ]);
    }
}
