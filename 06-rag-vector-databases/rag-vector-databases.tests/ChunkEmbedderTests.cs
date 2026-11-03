using System.Text.Json;
using Microsoft.Extensions.AI;
using ResumeRag.Tests.TestData;
using Xunit;

namespace ResumeRag.Tests;

public class ChunkEmbedderTests
{
    private const string BatchResponseString = """
        {"object":"list","model":"test-embedding","data":[
        {"object":"embedding","index":0,"embedding":[1,0]},
        {"object":"embedding","index":1,"embedding":[0,1]}],
        "usage":{"prompt_tokens":12,"total_tokens":12}}
        """;

    // A bad later input must stop both embedding batches at the same preparation boundary as Program.cs.
    [Theory]
    [InlineData("document", "Documents[project-f.md]")]
    [InlineData("question", "Questions:")]
    public async Task Invalid_later_input_prevents_all_embedding_requests(string invalidInputString, string expectedFieldString)
    {
        List<SourceDocument> sourceDocuments = await DocumentsLoader.LoadAsync(Path.Combine(AppContext.BaseDirectory, "SampleData"));
        List<RagQuestion> ragQuestions = RagFixtures.CreateQuestions();
        if (invalidInputString == "document")
        {
            sourceDocuments[^1] = sourceDocuments[^1] with
            {
                TextString = " "
            };
        }
        else
        {
            ragQuestions[^1] = ragQuestions[^1] with
            {
                TextString = ""
            };
        }

        using var embeddingResponseHandler = new EmbeddingResponseHandler(BatchResponseString);
        using var httpClient = new HttpClient(embeddingResponseHandler);
        using IEmbeddingGenerator<string, Embedding<float>> iEmbeddingGenerator = CreateGenerator(httpClient);

        InvalidDataException invalidDataException = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            InputsValidator.Validate(sourceDocuments, ragQuestions, 650, 80, 3, 0.2, 2400);
            List<DocumentChunk> documentChunks = MarkdownChunker.Chunk(sourceDocuments, 650, 80);
            List<string> questionTextStrings = ragQuestions.Select(ragQuestion => ragQuestion.TextString).ToList();
            List<string> documentTextStrings = documentChunks.Select(documentChunk => documentChunk.HeadingString + "\n" + documentChunk.TextString).ToList();
            await ChunkEmbedder.RequestAsync(iEmbeddingGenerator, documentTextStrings);
            await ChunkEmbedder.RequestAsync(iEmbeddingGenerator, questionTextStrings);
        });

        Assert.Contains(expectedFieldString, invalidDataException.Message);
        Assert.Empty(embeddingResponseHandler.RequestBodyStrings);
    }

    // Valid vectors with a different model ID still cannot be reused, even when their dimensions match.
    [Fact]
    public async Task Incompatible_index_prevents_all_embedding_requests()
    {
        List<SourceDocument> sourceDocuments = await DocumentsLoader.LoadAsync(Path.Combine(AppContext.BaseDirectory, "SampleData"));
        List<RagQuestion> ragQuestions = RagFixtures.CreateQuestions();
        ChunkIndex previousChunkIndex = RagFixtures.CreateIndex();
        previousChunkIndex = previousChunkIndex with
        {
            IndexMetadata = previousChunkIndex.IndexMetadata with
            {
                EmbeddingModelString = "other-model"
            }
        };
        using var embeddingResponseHandler = new EmbeddingResponseHandler(BatchResponseString);
        using var httpClient = new HttpClient(embeddingResponseHandler);
        using IEmbeddingGenerator<string, Embedding<float>> iEmbeddingGenerator = CreateGenerator(httpClient);

        InvalidDataException invalidDataException = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            InputsValidator.Validate(sourceDocuments, ragQuestions, 650, 80, 3, 0.2, 2400);
            List<DocumentChunk> documentChunks = MarkdownChunker.Chunk(sourceDocuments, 650, 80);
            List<string> questionTextStrings = ragQuestions.Select(ragQuestion => ragQuestion.TextString).ToList();
            IndexValidator.Validate(previousChunkIndex);
            IndexValidator.ValidateCompatibility(previousChunkIndex, "OpenRouter", "test-embedding", 650, 80);
            List<DocumentChunk> changedDocumentChunks = IndexPreparer.FindChanged(documentChunks, previousChunkIndex);
            List<string> documentTextStrings = changedDocumentChunks.Select(documentChunk => documentChunk.HeadingString + "\n" + documentChunk.TextString).ToList();
            if (changedDocumentChunks.Count > 0)
            {
                await ChunkEmbedder.RequestAsync(iEmbeddingGenerator, documentTextStrings);
            }

            await ChunkEmbedder.RequestAsync(iEmbeddingGenerator, questionTextStrings);
        });

        Assert.Contains("embedding model", invalidDataException.Message);
        Assert.Empty(embeddingResponseHandler.RequestBodyStrings);
    }

    // The SDK must send each prepared text once and pass a cancellable deadline through to HTTP.
    [Fact]
    public async Task Batch_request_preserves_prepared_texts_and_cancellation()
    {
        List<string> textsStrings = ["Container delivery\nMira used Docker.", "Relational storage\nMira used PostgreSQL."];
        using var embeddingResponseHandler = new EmbeddingResponseHandler(BatchResponseString);
        using var httpClient = new HttpClient(embeddingResponseHandler);
        using IEmbeddingGenerator<string, Embedding<float>> iEmbeddingGenerator = CreateGenerator(httpClient);

        GeneratedEmbeddings<Embedding<float>> generatedEmbeddings = await ChunkEmbedder.RequestAsync(iEmbeddingGenerator, textsStrings);
        EmbeddingValidator.Validate(generatedEmbeddings, textsStrings.Count, "test-embedding");

        using JsonDocument requestJsonDocument = JsonDocument.Parse(Assert.Single(embeddingResponseHandler.RequestBodyStrings));
        Assert.Equal(textsStrings,
            requestJsonDocument.RootElement.GetProperty("input").EnumerateArray().Select(textJsonElement => textJsonElement.GetString()));
        Assert.Equal("test-embedding", requestJsonDocument.RootElement.GetProperty("model").GetString());
        Assert.True(embeddingResponseHandler.HadCancelableTokenBool);
        Assert.Equal(new float[] { 1, 0 }, generatedEmbeddings[0].Vector.ToArray());
        Assert.Equal(new float[] { 0, 1 }, generatedEmbeddings[1].Vector.ToArray());
    }

    // Signal that HTTP has started before canceling an active request; no timing guesses are needed.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Caller_cancellation_stops_native_requests(bool cancelBeforeRequestBool)
    {
        using var callerCancellationTokenSource = new CancellationTokenSource();
        using var embeddingResponseHandler = new EmbeddingResponseHandler(BatchResponseString, holdResponseBool: true);
        using var httpClient = new HttpClient(embeddingResponseHandler);
        using IEmbeddingGenerator<string, Embedding<float>> iEmbeddingGenerator = CreateGenerator(httpClient);
        if (cancelBeforeRequestBool)
        {
            callerCancellationTokenSource.Cancel();
        }

        try
        {
            Task<GeneratedEmbeddings<Embedding<float>>> pendingGeneratedEmbeddingsTask = ChunkEmbedder.RequestAsync(iEmbeddingGenerator, ["Docker work."], callerCancellationTokenSource.Token);
            if (!cancelBeforeRequestBool)
            {
                await embeddingResponseHandler.RequestStartedTask.WaitAsync(TimeSpan.FromSeconds(10));
                callerCancellationTokenSource.Cancel();
            }
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pendingGeneratedEmbeddingsTask.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(cancelBeforeRequestBool ? 0 : 1, embeddingResponseHandler.RequestBodyStrings.Count);
            if (!cancelBeforeRequestBool)
            {
                Assert.True(embeddingResponseHandler.HadCancelableTokenBool);
            }
        }
        finally { callerCancellationTokenSource.Cancel(); }
    }

    // Use the real shared SDK adapter with fictional settings; the injected HTTP handler owns all responses.
    private static IEmbeddingGenerator<string, Embedding<float>> CreateGenerator(HttpClient httpClient)
    {
        return new AiClientFactory().CreateEmbeddingGenerator(new("OpenRouter", "test-key", "test-embedding", new("https://example.invalid/v1")), httpClient);
    }
}
