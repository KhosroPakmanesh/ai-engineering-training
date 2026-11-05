using System.Text.Json.Serialization;

namespace ResumeMcpServer;

// These facts belong to a fictional candidate; JSON names are the MCP data contract.
public sealed record ResumeProfile(
    [property: JsonPropertyName("id")] string IdString,
    [property: JsonPropertyName("name")] string NameString,
    [property: JsonPropertyName("summary")] string SummaryString,
    [property: JsonPropertyName("skills")] List<string> SkillStrings);
