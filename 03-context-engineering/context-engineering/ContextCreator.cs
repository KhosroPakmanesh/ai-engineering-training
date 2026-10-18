using System.Text.Json;

public static class ContextCreator
{
    // Prepare both context choices together so neither request precedes prompt checks.
    public static List<PreparedContext> CreateContextStrategies(ComparisonInputs inputs)
    {
        List<ContextStrategy> contexts =
        [
            FullContextStrategy.Prepare(inputs.Sections),
            SelectedContextStrategy.Prepare(inputs.Sections, inputs.Question)
        ];

        return PrepareContext(contexts, inputs.Question);
    }
    // Reject oversized prompts instead of silently changing the supplied evidence.
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
    // Package the question and unchanged section text as data for the shared instruction.
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

    // Both strategies preserve complete source sections and enforce the same size cap.
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
