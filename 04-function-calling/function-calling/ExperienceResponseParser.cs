using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

public static class ExperienceResponseParser
{
    // AskAsync checks the final answer, and the caller validates tool execution before parsing.
    public static ParsedExperienceResponse Parse(AgentResponse agentResponse)
    {
        string answerString = agentResponse.Messages.Last(chatMessage => chatMessage.Role == ChatRole.Assistant).Text;
        List<FunctionResultContent> toolFunctionResultContents = agentResponse.Messages
            .SelectMany(chatMessage => chatMessage.Contents)
            .OfType<FunctionResultContent>()
            .ToList();

        ExperienceResult experienceResult = JsonSerializer.SerializeToElement(toolFunctionResultContents[0].Result)
            .Deserialize<ExperienceResult>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("The calculation returned no structured result.");

        // Each completed Chat Completions turn contributes one assistant message.
        int modelCallCountInt = agentResponse.Messages.Count(chatMessage => chatMessage.Role == ChatRole.Assistant);
        return new ParsedExperienceResponse(AnswerString: answerString, ExperienceResult: experienceResult,
            ModelCallCountInt: modelCallCountInt, ToolExecutionCountInt: toolFunctionResultContents.Count);
    }
}
