using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;

public class ExperienceResponseParserTests
{
    // Read the actual result and final answer without including the tool-selection preamble.
    [Fact]
    public void Parsing_reads_the_final_answer_calculation_and_call_counts()
    {
        // Arrange
        JsonElement toolResultJsonElement = JsonSerializer.Deserialize<JsonElement>("""
            {
              "totalMonths": 7,
              "inputIntervalCount": 2,
              "mergedIntervals": [{ "startDate": "2023-01-01", "endDate": "2023-08-01" }]
            }
            """);
        AgentResponse agentResponse = CreateResponse(toolResultJsonElement);

        // Act
        ParsedExperienceResponse parsedExperienceResponse = ExperienceResponseParser.Parse(agentResponse);

        // Assert
        Assert.Equal(
            "7 full calendar months [CalculateExperience.totalMonths].",
            parsedExperienceResponse.AnswerString);
        Assert.Equal(
            7,
            parsedExperienceResponse.ExperienceResult.TotalMonthsInt);
        Assert.Equal(
            2,
            parsedExperienceResponse.ExperienceResult.InputIntervalCount);
        MergedWorkInterval mergedWorkInterval =
            Assert.Single(parsedExperienceResponse.ExperienceResult.MergedWorkIntervals);
        Assert.Equal(
            new DateOnly(
                2023,
                1,
                1),
            mergedWorkInterval.StartDateOnly);
        Assert.Equal(
            new DateOnly(
                2023,
                8,
                1),
            mergedWorkInterval.EndDateOnly);
        Assert.Equal(
            2,
            parsedExperienceResponse.ModelCallCountInt);
        Assert.Equal(
            1,
            parsedExperienceResponse.ToolExecutionCountInt);
    }

    // An executed function still needs a structured calculation result to parse.
    [Fact]
    public void A_null_calculation_result_is_rejected()
    {
        // Arrange
        AgentResponse agentResponse = CreateResponse(resultObject: null);

        // Act
        InvalidDataException errorInvalidDataException = Assert.Throws<InvalidDataException>(() =>
            ExperienceResponseParser.Parse(agentResponse));

        // Assert
        Assert.Contains(
            "no structured result",
            errorInvalidDataException.Message);
    }

    // Each case gets a fresh conversation with one calculator result and two assistant messages.
    private static AgentResponse CreateResponse(object? resultObject)
    {
        return new(
        [
            new ChatMessage(
            ChatRole.Assistant,
            [
                new TextContent("I will use the calculator."),
                new FunctionCallContent(
                "calculation-0",
                "CalculateExperience",
                new Dictionary<string, object?>())
            ]),
            new ChatMessage(
            ChatRole.Tool,
            [
                new FunctionResultContent(
                "calculation-0",
                resultObject)
            ]),
            new ChatMessage(
            ChatRole.Assistant,
            "7 full calendar months [CalculateExperience.totalMonths].")
        ]);
    }
}
