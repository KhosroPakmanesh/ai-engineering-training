using ModelContextProtocol.Client;

namespace ResumeMcpClient;

public static class InputsValidator
{
    // Complete all local preparation before the first paid request.
    public static void Validate(
        string questionString,
        IEnumerable<McpClientTool> mcpClientTools,
        ResumeDraft resumeDraft,
        List<string> directSkillStrings)
    {
        ValidateQuestion(questionString);
        ValidateTools(mcpClientTools);
        ValidateDirectSkills(
            resumeDraft,
            directSkillStrings);
    }

    // A blank question does not define a useful experiment.
    private static void ValidateQuestion(string questionString)
    {
        if (string.IsNullOrWhiteSpace(questionString))
        {
            throw new InvalidDataException("Provide a nonempty skills question.");
        }
    }

    // A different server could advertise a different contract under the same process name.
    private static void ValidateTools(IEnumerable<McpClientTool> mcpClientTools)
    {
        if (!mcpClientTools.Select(mcpClientTool => mcpClientTool.Name).Order()
            .SequenceEqual(new[] { "get_profile", "list_skills", "save_resume" }))
        {
            throw new InvalidDataException("The server must expose exactly get_profile, list_skills, and save_resume.");
        }
    }

    // Both direct source tools must agree before the agent gets a chance to use either one.
    private static void ValidateDirectSkills(
        ResumeDraft resumeDraft,
        List<string> directSkillStrings)
    {
        if (!directSkillStrings.SequenceEqual(resumeDraft.SkillStrings))
        {
            throw new InvalidDataException("The direct MCP skills do not match the selected profile.");
        }
    }
}
