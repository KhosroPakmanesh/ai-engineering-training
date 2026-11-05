using System.Text.Json;

namespace ResumeMcpClient;

public static class ResumePreparer
{
    // A server reply is another trust boundary. Check its fields before preparing a model request.
    public static ResumeDraft Prepare(
        JsonElement profileJsonElement,
        string expectedProfileIdString)
    {
        string? idString = profileJsonElement.GetProperty("id").GetString();
        string? nameString = profileJsonElement.GetProperty("name").GetString();
        string? summaryString = profileJsonElement.GetProperty("summary").GetString();
        List<string>? skillStrings = profileJsonElement.GetProperty("skills").Deserialize<List<string>>();
        if (idString != expectedProfileIdString || string.IsNullOrWhiteSpace(nameString)
            || string.IsNullOrWhiteSpace(summaryString) ||
            skillStrings is null || skillStrings.Count == 0
            || skillStrings.Any(skillString => string.IsNullOrWhiteSpace(skillString)))
        {
            throw new InvalidDataException(
                "The MCP profile must match the requested ID and contain a name, summary, and nonempty skills.");
        }

        string markdownString = $"# {nameString}\n\n{summaryString}\n\n## Skills\n\n" +
            string.Join(
                "\n",
                skillStrings.Select(skillString => "- " + skillString)) + "\n";
        return new(
            idString,
            skillStrings,
            markdownString);
    }
}
