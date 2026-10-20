// Keep lesson prompts and settings separate from the AI library that sends the request.
public sealed record AiRequest(string Name, string Instructions, string Input, float Temperature,
    int MaxOutputTokens, AiJsonSchema? ResponseSchema = null)
{
    public AiToolCalling? ToolCalling { get; init; }
}

public sealed record AiJsonSchema(string Name, string Schema);
