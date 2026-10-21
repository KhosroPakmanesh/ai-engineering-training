using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

public static class ToolExecutionValidator
{
    // Only a successful framework function result proves that the calculator really ran.
    public static void ValidateToolExecution(AgentResponse response)
    {
        List<FunctionResultContent> results = response.Messages.SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>().ToList();
        if (results.Count == 0)
            throw new InvalidDataException("The model did not execute CalculateExperience. No experience total was accepted.");
        if (results.Count != 1)
            throw new InvalidDataException("Expected exactly one CalculateExperience execution.");

        FunctionResultContent result = results[0];
        List<FunctionCallContent> calls = response.Messages.SelectMany(message => message.Contents)
            .OfType<FunctionCallContent>().Where(call => call.CallId == result.CallId).ToList();
        if (calls.Count != 1 || calls[0].Name != ExperienceAssistant.ToolName || result.Exception is not null)
            throw new InvalidDataException("Expected exactly one successful CalculateExperience execution.");
    }
}

