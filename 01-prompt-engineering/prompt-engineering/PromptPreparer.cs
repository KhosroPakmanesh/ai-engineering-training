public static class PromptPreparer
{
    // Package every checked instruction with the same resume before the first model call.
    public static List<PreparedPrompt> Prepare(ComparisonInputs comparisonInputs)
    {
        return comparisonInputs.PromptCases
            .Select(promptCase => new PreparedPrompt(
                promptCase.NameString,
                promptCase.PathString,
                $"{promptCase.InstructionString}\n\nRESUME DATA:\n{comparisonInputs.ResumeString}"))
            .ToList();
    }
}
