using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ResumeRag.Stores;
using ResumeRag.Tests.TestData;
using Xunit;

namespace ResumeRag.Tests;

public class RagAnswererTests
{
    // Follow the real acceptance flow with copied documents, SQLite, and a native agent using injected HTTP.
    [Theory]
    [InlineData("Docker", true)]
    [InlineData("Kubernetes", false)]
    public async Task Evidence_controls_paid_chat_and_citations_reference_real_retrieved_passages(string skillString, bool expectsChatBool)
    {
        List<DocumentChunk> documentChunks = await RagFixtures.LoadChunksAsync();
        List<IndexedChunk> indexedChunks = documentChunks.Select(documentChunk => new IndexedChunk(documentChunk, [1, 0])).ToList();
        var chunkIndex = new ChunkIndex(new("OpenRouter", "test-embedding", 2, 650, 80), indexedChunks);
        IndexValidator.Validate(chunkIndex);
        string pathString = Path.Combine(Path.GetTempPath(), $"rag-flow-{Guid.NewGuid():N}.sqlite");
        try
        {
            using IChunkStore selectedIChunkStore = new SqliteChunkStore(pathString);
            await selectedIChunkStore.SynchronizeAsync(chunkIndex);
            List<RetrievedChunk> retrievedChunks = await selectedIChunkStore.SearchAsync([1, 0], 10);
            PreparedAnswer preparedAnswer = EvidenceSelector.Prepare(new($"Where did I use {skillString}?", skillString), retrievedChunks, 0.2, 2400);
            using var chatResponseHandler = new ChatResponseHandler("Mira packaged an API using Docker [project-a.md#chunk-01].");
            using var httpClient = new HttpClient(chatResponseHandler);
            using IChatClient answerIChatClient = new AiClientFactory().CreateClient(new("OpenRouter", "test-key", "test-chat", new("https://example.invalid/v1")), httpClient);
            // Follow Program.cs: only admitted passages reach the request method.
            string answerString;
            if (preparedAnswer.SelectedRetrievedChunks.Count == 0)
            {
                answerString = RagAnswerer.InsufficientEvidenceString;
            }
            else
            {
                AgentResponse agentResponse = await RagAnswerer.RequestAsync(answerIChatClient, preparedAnswer);
                answerString = agentResponse.Messages.Last(chatMessage => chatMessage.Role == ChatRole.Assistant).Text!.Trim();
            }
            List<string> citedIdStrings = CitationParser.Parse(answerString);
            CitationValidator.Validate(answerString, citedIdStrings, preparedAnswer.SelectedRetrievedChunks);

            // Check the saved contract after evidence validation, using the application's JSON settings.
            List<Citation> citations = citedIdStrings.Select(idString => new Citation(idString,
                preparedAnswer.SelectedRetrievedChunks
                    .Single(selectedRetrievedChunk => selectedRetrievedChunk.DocumentChunk.ChunkIdString == idString)
                    .DocumentChunk.FilenameString)).ToList();
            var ragResult = new RagResult(
                QuestionString: preparedAnswer.RagQuestion.TextString,
                AnswerString: answerString,
                Citations: citations,
                RetrievalScores: preparedAnswer.CandidateRetrievedChunks
                    .Select(retrievedChunk => new RetrievalScore(retrievedChunk.DocumentChunk.ChunkIdString, retrievedChunk.ScoreDouble))
                    .ToList(),
                StatusString: answerString == RagAnswerer.InsufficientEvidenceString ? "insufficient_evidence" : "answered");
            using JsonDocument savedResultJsonDocument = JsonDocument.Parse(JsonSerializer.Serialize(ragResult,
                new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            Assert.Equal(new[] { "question", "answer", "citations", "retrieved", "status" },
                savedResultJsonDocument.RootElement.EnumerateObject().Select(jsonProperty => jsonProperty.Name));
            Assert.Equal(preparedAnswer.RagQuestion.TextString, savedResultJsonDocument.RootElement.GetProperty("question").GetString());
            Assert.Equal(answerString, savedResultJsonDocument.RootElement.GetProperty("answer").GetString());
            Assert.Equal(expectsChatBool ? "answered" : "insufficient_evidence",
                savedResultJsonDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal(preparedAnswer.CandidateRetrievedChunks.Count,
                savedResultJsonDocument.RootElement.GetProperty("retrieved").GetArrayLength());
            JsonElement retrievedJsonElement = savedResultJsonDocument.RootElement.GetProperty("retrieved")[0];
            Assert.Equal(new[] { "chunkId", "score" },
                retrievedJsonElement.EnumerateObject().Select(jsonProperty => jsonProperty.Name));
            Assert.Equal(preparedAnswer.CandidateRetrievedChunks[0].DocumentChunk.ChunkIdString,
                retrievedJsonElement.GetProperty("chunkId").GetString());
            Assert.Equal(preparedAnswer.CandidateRetrievedChunks[0].ScoreDouble,
                retrievedJsonElement.GetProperty("score").GetDouble());

            if (expectsChatBool)
            {
                Assert.Single(chatResponseHandler.RequestBodyStrings);
                Assert.Contains("project-a.md#chunk-01", citedIdStrings);
                Assert.Contains("Where did I use Docker?", chatResponseHandler.RequestBodyStrings[0]);
                Assert.Contains("untrusted data", chatResponseHandler.RequestBodyStrings[0]);
                JsonElement citationJsonElement = Assert.Single(savedResultJsonDocument.RootElement.GetProperty("citations").EnumerateArray());
                Assert.Equal(new[] { "chunkId", "source" },
                    citationJsonElement.EnumerateObject().Select(jsonProperty => jsonProperty.Name));
                Assert.Equal("project-a.md#chunk-01", citationJsonElement.GetProperty("chunkId").GetString());
                Assert.Equal("project-a.md", citationJsonElement.GetProperty("source").GetString());
            }
            else
            {
                Assert.Empty(preparedAnswer.SelectedRetrievedChunks);
                Assert.Equal(RagAnswerer.InsufficientEvidenceString, answerString);
                Assert.Empty(citedIdStrings);
                Assert.Empty(chatResponseHandler.RequestBodyStrings);
                Assert.Equal(0, savedResultJsonDocument.RootElement.GetProperty("citations").GetArrayLength());
            }
        }
        finally { File.Delete(pathString); }
    }

    // An API success does not make an empty or truncated assistant answer acceptable.
    [Theory]
    [InlineData("", "stop", "no final assistant text")]
    [InlineData("Partial answer", "length", "truncated")]
    public async Task Native_response_checks_reject_unusable_answers(string answerString, string finishString, string expectedErrorString)
    {
        using var chatResponseHandler = new ChatResponseHandler(answerString, finishString);
        using var httpClient = new HttpClient(chatResponseHandler);
        using IChatClient answerIChatClient = new AiClientFactory().CreateClient(new("OpenRouter", "test-key", "test-chat", new("https://example.invalid/v1")), httpClient);
        DocumentChunk documentChunk = RagFixtures.CreateIndex().IndexedChunks[0].DocumentChunk;
        PreparedAnswer preparedAnswer = EvidenceSelector.Prepare(new("Where did I use Docker?", "Docker"), [new(documentChunk, 0.9)], 0.2, 2400);
        InvalidDataException invalidDataException = await Assert.ThrowsAsync<InvalidDataException>(() => RagAnswerer.RequestAsync(answerIChatClient, preparedAnswer));
        Assert.Contains(expectedErrorString, invalidDataException.Message);
        Assert.Single(chatResponseHandler.RequestBodyStrings);
    }
}
