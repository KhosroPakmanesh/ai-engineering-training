namespace ResumeMcpClient;

// These counts come from native messages, not estimates of provider token usage.
public sealed record SkillAnswer(
    string AnswerString,
    int ModelCallCountInt,
    int ToolExecutionCountInt);
