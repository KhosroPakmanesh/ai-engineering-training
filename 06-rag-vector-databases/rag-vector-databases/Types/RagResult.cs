using System.Text.Json.Serialization;

namespace ResumeRag;

// Keep the original JSON field names while code members show their full data types.
public sealed record RagResult(
    [property: JsonPropertyName("question")] string QuestionString,
    [property: JsonPropertyName("answer")] string AnswerString,
    [property: JsonPropertyName("citations")] List<Citation> Citations,
    [property: JsonPropertyName("retrieved")] List<RetrievalScore> RetrievalScores,
    [property: JsonPropertyName("status")] string StatusString);
