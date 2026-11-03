using System.Text.Json.Serialization;

namespace ResumeRag;
// Keep a question, every retrieved candidate, admitted evidence, and the complete prompt together.
public sealed record PreparedAnswer([property: JsonPropertyName("Question")] RagQuestion RagQuestion,
    [property: JsonPropertyName("Candidates")] List<RetrievedChunk> CandidateRetrievedChunks,
    [property: JsonPropertyName("Passages")] List<RetrievedChunk> SelectedRetrievedChunks,
    [property: JsonPropertyName("Prompt")] string PromptString);
