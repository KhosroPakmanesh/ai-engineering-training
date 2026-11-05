namespace ResumeMcpClient;

// Keep the known skills beside the exact source-based Markdown the user will review.
public sealed record ResumeDraft(
    string ProfileIdString,
    List<string> SkillStrings,
    string MarkdownString);
