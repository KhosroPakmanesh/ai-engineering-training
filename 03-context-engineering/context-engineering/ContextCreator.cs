using System.Text.Json;

public static class ContextCreator
{
    // Prepare and check both prompts before making either model call.
    public static List<PreparedContext> CreateContextStrategies(ComparisonInputs inputs)
    {
        List<ContextStrategy> contexts =
        [
            FullContextStrategy.Prepare(inputs.Sections),
            SelectedContextStrategy.Prepare(inputs.Sections, inputs.Question)
        ];

        return PrepareContext(contexts, inputs.Question);
    }
    // Stop if a prompt is too long; do not remove source text to make it fit.
    private static List<PreparedContext> PrepareContext(List<ContextStrategy> selectedContext, string question)
    {
        List<PreparedContext> preparedContext = selectedContext
            .Select(context => new PreparedContext(context, BuildContext(question, context))).ToList();

        foreach (PreparedContext item in preparedContext)
        {
            if (ContextCreatorHelpers.SystemInstructions.Length + item.Prompt.Length > ContextCreatorHelpers.PromptCharacterCap)
                throw new InvalidDataException
                    ($"The complete prompt exceeds {ContextCreatorHelpers.PromptCharacterCap} characters. " +
                    $"No requests were made or content clipped.");
        }

        return preparedContext;
    }
    // Put the question and original project text in JSON so the model receives them as data.
    private static string BuildContext(string question, ContextStrategy context)
    {
        JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

        return JsonSerializer.Serialize(new
        {
            question,
            sections = context.Sections.Select(section => new
            {
                id = section.Id,
                heading = section.Heading,
                text = section.Text
            })
        }, jsonOptions);
    }

    // Keep each selected section whole and stop if the combined text exceeds the limit.
    public static ContextStrategy CreateContext(List<ProjectSection> included, string strategy, int characterCap)
    {
        string context = string.Join("\n\n", included.Select(section => section.Text));

        if (context.Length > characterCap)
            throw new InvalidDataException(
                $"{strategy} context has {context.Length} characters; the cap is {characterCap}. " +
                $"Shorten the source document. No content was clipped.");

        return new(strategy, included, context);
    }
}
