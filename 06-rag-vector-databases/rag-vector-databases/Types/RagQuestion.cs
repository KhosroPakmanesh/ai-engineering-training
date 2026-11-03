using System.Text.Json.Serialization;

namespace ResumeRag;
// Each question names the skill required by the local evidence check.
public sealed record RagQuestion([property: JsonPropertyName("Text")] string TextString,
    [property: JsonPropertyName("Skill")] string SkillString);
