using System.Text.Json.Serialization;

namespace ResumeRag;

// Keep the original JSON field names while code members show their full data types.
public sealed record PreparedAnswer(
    [property: JsonPropertyName("Question")] RagQuestion RagQuestion,
    [property: JsonPropertyName("Candidates")] List<RetrievedChunk> CandidateRetrievedChunks,
    [property: JsonPropertyName("Passages")] List<RetrievedChunk> SelectedRetrievedChunks,
    [property: JsonPropertyName("Prompt")] string PromptString);
