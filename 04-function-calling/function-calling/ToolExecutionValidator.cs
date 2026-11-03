using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

public static class ToolExecutionValidator
{
    // Only a successful framework function result proves that the calculator really ran.
    public static void ValidateToolExecution(AgentResponse agentResponse)
    {
        List<FunctionResultContent> functionResultContents = agentResponse.Messages.SelectMany(chatMessage => chatMessage.Contents)
            .OfType<FunctionResultContent>().ToList();
        if (functionResultContents.Count == 0)
        {
            throw new InvalidDataException("The model did not execute CalculateExperience. No experience total was accepted.");
        }

        if (functionResultContents.Count != 1)
        {
            throw new InvalidDataException("Expected exactly one CalculateExperience execution.");
        }

        FunctionResultContent resultFunctionResultContent = functionResultContents[0];
        List<FunctionCallContent> callsFunctionCallContents = agentResponse.Messages.SelectMany(chatMessage => chatMessage.Contents)
            .OfType<FunctionCallContent>().Where(callFunctionCallContent => callFunctionCallContent.CallId == resultFunctionResultContent.CallId).ToList();
        if (callsFunctionCallContents.Count != 1 || callsFunctionCallContents[0].Name != ExperienceAssistant.ToolNameString || resultFunctionResultContent.Exception is not null)
        {
            throw new InvalidDataException("Expected exactly one successful CalculateExperience execution.");
        }
    }
}
