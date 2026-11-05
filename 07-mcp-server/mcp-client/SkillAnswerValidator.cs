using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ResumeMcpClient;

public static class SkillAnswerValidator
{
    // Matching text alone cannot establish tool use; check the native call and successful result too.
    public static void Validate(
        AgentResponse agentResponse,
        SkillAnswer skillAnswer,
        List<string> expectedSkillStrings)
    {
        ValidateToolExecution(
            agentResponse,
            skillAnswer);
        ValidateAnswer(
            skillAnswer,
            expectedSkillStrings);
    }

    // A matching call ID ties the successful result to the requested tool.
    private static void ValidateToolExecution(
        AgentResponse agentResponse,
        SkillAnswer skillAnswer)
    {
        List<FunctionCallContent> functionCallContents = agentResponse.Messages
            .SelectMany(chatMessage =>
            chatMessage.Contents)
            .OfType<FunctionCallContent>()
            .ToList();
        List<FunctionResultContent> functionResultContents = agentResponse.Messages
            .SelectMany(chatMessage =>
            chatMessage.Contents)
            .OfType<FunctionResultContent>()
            .ToList();
        if (skillAnswer.ModelCallCountInt != ResumeAssistant.MaxModelCallsInt || skillAnswer.ToolExecutionCountInt != 1
            ||
            functionCallContents.Count != 1 || functionCallContents[0].Name != "list_skills" ||
            functionResultContents.Count != 1 || functionResultContents[0].Exception is not null ||
            functionResultContents[0].CallId != functionCallContents[0].CallId)
        {
            throw new InvalidDataException(
                "Expected two model calls and exactly one successful list_skills MCP execution.");
        }
    }

    // A narrow answer contract prevents a correct list from hiding additional unsupported claims.
    private static void ValidateAnswer(
        SkillAnswer skillAnswer,
        List<string> expectedSkillStrings)
    {
        string expectedAnswerString = "Skills: " + string.Join(
            ", ",
            expectedSkillStrings) + " [list_skills]";
        if (skillAnswer.AnswerString != expectedAnswerString)
        {
            throw new InvalidDataException(
                "The final answer must copy all source skills in order and cite [list_skills], with no extra claims.");
        }
    }
}
