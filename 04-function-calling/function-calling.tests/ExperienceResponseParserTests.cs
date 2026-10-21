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
        JsonElement toolResult = JsonSerializer.Deserialize<JsonElement>("""
            {
              "totalMonths": 7,
              "inputIntervalCount": 2,
              "mergedIntervals": [{ "startDate": "2023-01-01", "endDate": "2023-08-01" }]
            }
            """);
        AgentResponse response = CreateResponse(toolResult);

        ParsedExperienceResponse parsedResponse = ExperienceResponseParser.Parse(response);

        Assert.Equal("7 full calendar months [CalculateExperience.totalMonths].", parsedResponse.Answer);
        Assert.Equal(7, parsedResponse.Result.TotalMonths);
        Assert.Equal(2, parsedResponse.Result.InputIntervalCount);
        MergedWorkInterval interval = Assert.Single(parsedResponse.Result.MergedIntervals);
        Assert.Equal(new DateOnly(2023, 1, 1), interval.StartDate);
        Assert.Equal(new DateOnly(2023, 8, 1), interval.EndDate);
        Assert.Equal(2, parsedResponse.ModelCallCount);
        Assert.Equal(1, parsedResponse.ToolExecutionCount);
    }

    // An executed function still needs a structured calculation result to parse.
    [Fact]
    public void A_null_calculation_result_is_rejected()
    {
        AgentResponse response = CreateResponse(result: null);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => ExperienceResponseParser.Parse(response));

        Assert.Contains("no structured result", error.Message);
    }

    // Each case gets a fresh conversation with one calculator result and two assistant messages.
    private static AgentResponse CreateResponse(object? result) => new(
    [
        new ChatMessage(ChatRole.Assistant,
        [
            new TextContent("I will use the calculator."),
            new FunctionCallContent("calculation-0", "CalculateExperience", new Dictionary<string, object?>())
        ]),
        new ChatMessage(ChatRole.Tool, [new FunctionResultContent("calculation-0", result)]),
        new ChatMessage(ChatRole.Assistant, "7 full calendar months [CalculateExperience.totalMonths].")
    ]);
}
