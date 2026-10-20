// Return the answer, token counts, and request time using the same fields for every provider.
public sealed record AiResponse(string Text, AiTokenUsage? Usage, long LatencyMilliseconds)
{
    public int ModelCalls { get; init; } = 1;
    public List<AiToolExecution> ToolExecutions { get; init; } = [];
}

public sealed record AiTokenUsage(long? InputTokens, long? OutputTokens, long? TotalTokens);
