
public static class ToolExecutionValidator
{
    // A model saying it used a tool is insufficient; require the facade's actual execution record.
    public static void ValidateToolExecution(AiResponse response)
    {
        if (response.ToolExecutions.Count == 0)
            throw new InvalidDataException("The model did not execute CalculateExperience. No experience total was accepted.");
        if (response.ToolExecutions.Count != 1 || response.ToolExecutions[0].ToolName != ExperienceAssistant.ToolName)
            throw new InvalidDataException("Expected exactly one CalculateExperience execution.");
    }
}

