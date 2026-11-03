using System.Text.Json.Serialization;

namespace ResumeRag;
// An accepted answer retains its citations, retrieval scores, and evidence status.
public sealed record RagResult([property: JsonPropertyName("question")] string QuestionString,
    [property: JsonPropertyName("answer")] string AnswerString, [property: JsonPropertyName("citations")] List<Citation> Citations,
    [property: JsonPropertyName("retrieved")] List<RetrievalScore> RetrievalScores,
    [property: JsonPropertyName("status")] string StatusString);
