using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ResumeMcpClient;

public static class SkillAnswerParser
{
    // Earlier assistant preambles are not part of the answer after the MCP tool returns.
    public static SkillAnswer Parse(AgentResponse agentResponse)
    {
        List<ChatMessage> assistantChatMessages = agentResponse.Messages.Where(chatMessage =>
            chatMessage.Role == ChatRole.Assistant).ToList();
        string answerString = assistantChatMessages.Last().Text!.Trim();
        int toolExecutionCountInt = agentResponse.Messages
            .SelectMany(chatMessage =>
            chatMessage.Contents)
            .OfType<FunctionResultContent>().Count();
        return new(
            answerString,
            assistantChatMessages.Count,
            toolExecutionCountInt);
    }
}
