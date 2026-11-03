using System.Text.Json;

public static class ContextCreator
{
    // Prepare and check both prompts before making either model call.
    public static List<PreparedContext> CreateContextStrategies(ComparisonInputs comparisonInputs)
    {
        List<ContextStrategy> contextStrategies =
        [
            FullContextStrategy.Prepare(comparisonInputs.ProjectSections),
            SelectedContextStrategy.Prepare(comparisonInputs.ProjectSections, comparisonInputs.QuestionString)
        ];

        return PrepareContext(contextStrategies, comparisonInputs.QuestionString);
    }

    // Stop if a prompt is too long; do not remove source text to make it fit.
    private static List<PreparedContext> PrepareContext(List<ContextStrategy> selectedContextStrategies, string questionString)
    {
        List<PreparedContext> preparedContexts = selectedContextStrategies
            .Select(contextStrategy => new PreparedContext(contextStrategy, BuildContext(questionString, contextStrategy))).ToList();

        foreach (PreparedContext preparedContext in preparedContexts)
        {
            if (ContextCreatorHelpers.SystemInstructionsString.Length + preparedContext.PromptString.Length > ContextCreatorHelpers.PromptCharacterCapInt)
            {
                throw new InvalidDataException(
                    $"The complete prompt exceeds {ContextCreatorHelpers.PromptCharacterCapInt} characters. " +
                    "No requests were made or content clipped.");
            }
        }

        return preparedContexts;
    }

    // Put the question and original project text in JSON so the model receives them as data.
    private static string BuildContext(string questionString, ContextStrategy contextStrategy)
    {
        JsonSerializerOptions jsonSerializerOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        return JsonSerializer.Serialize(
            new
            {
                question = questionString,
                sections = contextStrategy.ProjectSections.Select(projectSection => new
                {
                    id = projectSection.IdString,
                    heading = projectSection.HeadingString,
                    text = projectSection.TextString
                })
            },
            jsonSerializerOptions);
    }

    // Keep each selected section whole and stop if the combined text exceeds the limit.
    public static ContextStrategy CreateContext(List<ProjectSection> includedProjectSections, string strategyString, int characterCapInt)
    {
        string contextString = string.Join("\n\n", includedProjectSections.Select(projectSection => projectSection.TextString));

        if (contextString.Length > characterCapInt)
        {
            throw new InvalidDataException(
                $"{strategyString} context has {contextString.Length} characters; the cap is {characterCapInt}. " +
                "Shorten the source document. No content was clipped.");
        }

        return new(strategyString, includedProjectSections, contextString);
    }
}
