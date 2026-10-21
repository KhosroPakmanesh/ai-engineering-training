using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

public static class ExperienceResponseParser
{
    // AskAsync checks the final answer, and the caller validates tool execution before parsing.
    public static ParsedExperienceResponse Parse(AgentResponse response)
    {
        string answer = response.Messages.Last(message => message.Role == ChatRole.Assistant).Text;
        List<FunctionResultContent> toolResults = response.Messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .ToList();

        ExperienceResult result = JsonSerializer.SerializeToElement(toolResults[0].Result)
            .Deserialize<ExperienceResult>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("The calculation returned no structured result.");

        // Each completed Chat Completions turn contributes one assistant message.
        int modelCallCount = response.Messages.Count(message => message.Role == ChatRole.Assistant);
        return new ParsedExperienceResponse(
            Answer: answer,
            Result: result,
            ModelCallCount: modelCallCount,
            ToolExecutionCount: toolResults.Count);
    }
}
