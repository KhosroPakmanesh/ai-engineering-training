using System.Text.Json.Serialization;

// Expected project IDs let the lesson measure whether supporting sources were included.
public record QuestionFixture(
    [property: JsonPropertyName("question")] string QuestionString,
    [property: JsonPropertyName("expectedSectionIds")] List<string> ExpectedSectionIdStrings);
