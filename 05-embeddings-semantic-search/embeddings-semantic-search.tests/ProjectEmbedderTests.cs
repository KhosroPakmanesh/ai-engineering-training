using System.Text.Json;
using Microsoft.Extensions.AI;
using Xunit;

public class ProjectEmbedderTests
{
    // Every project must pass local validation before any request reaches the HTTP boundary.
    [Fact]
    public async Task Invalid_later_project_prevents_all_remote_requests()
    {
        List<ProjectDescription> projects = await EmbeddingTestData.LoadAsync();
        projects[^1] = projects[^1] with { Text = " " };
        using var handler = new EmbeddingResponseHandler(EmbeddingTestData.Response([[1, 0]]));
        using var httpClient = new HttpClient(handler);
        using IEmbeddingGenerator<string, Embedding<float>> generator = CreateGenerator(httpClient);

        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            SearchInputsValidator.Validate(projects, "containerized APIs", 5);
            List<string> texts = projects.Select(project => project.Text).ToList();
            await ProjectEmbedder.RequestAsync(generator, texts);
        });

        Assert.Contains("projects[11].text", error.Message);
        Assert.Empty(handler.RequestBodies);
    }

    // The native request must preserve the complete prepared batch and pass its deadline token to HTTP.
    [Fact]
    public async Task Batch_request_preserves_original_text_and_cancellation()
    {
        List<string> texts = ["Docker service", "Garden budget"];
        using var handler = new EmbeddingResponseHandler(EmbeddingTestData.Response([[1, 0], [0, 1]]));
        using var httpClient = new HttpClient(handler);

        using IEmbeddingGenerator<string, Embedding<float>> generator = CreateGenerator(httpClient);
        GeneratedEmbeddings<Embedding<float>> response = await ProjectEmbedder.RequestAsync(generator, texts);
        EmbeddingResponseValidator.Validate(response, 2, "test-model");

        using JsonDocument request = JsonDocument.Parse(Assert.Single(handler.RequestBodies));
        Assert.Equal(texts, request.RootElement.GetProperty("input").EnumerateArray().Select(value => value.GetString()));
        Assert.True(handler.HadCancelableToken);
    }

    // Cancellation should stop an active embedding request as well as one canceled before it starts.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Caller_cancellation_stops_native_requests(bool cancelBeforeRequest)
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new EmbeddingResponseHandler(EmbeddingTestData.Response([[1, 0]]), cancellation.Cancel);
        using var httpClient = new HttpClient(handler);
        using IEmbeddingGenerator<string, Embedding<float>> generator = CreateGenerator(httpClient);
        if (cancelBeforeRequest) cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ProjectEmbedder.RequestAsync(generator, ["Docker service"], cancellation.Token));

        Assert.Equal(cancelBeforeRequest ? 0 : 1, handler.RequestBodies.Count);
    }

    // Follow the application's complete acceptance flow using the real selected provider and copied corpus.
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Live_corpus_and_query_pass_validation_storage_and_both_rankings()
    {
        const string query = "experience building containerized APIs";
        const int topK = 5;
        List<ProjectDescription> projects = await EmbeddingTestData.LoadAsync();
        SearchInputsValidator.Validate(projects, query, topK);
        List<string> documentTexts = projects.Select(project => project.Text).ToList();
        List<string> queryTexts = [query];
        List<SearchResult> lexicalResults = LexicalSearch.Rank(projects, query, topK);
        ProviderSettings settings = await SettingsReader.ReadSettingsAsync(AppContext.BaseDirectory, useEmbeddingModel: true);
        using IEmbeddingGenerator<string, Embedding<float>> generator = new AiClientFactory().CreateEmbeddingGenerator(settings);
        string path = Path.Combine(Path.GetTempPath(), $"embedding-integration-{Guid.NewGuid():N}.sqlite");
        try
        {
            GeneratedEmbeddings<Embedding<float>> documentEmbeddings = await ProjectEmbedder.RequestAsync(generator, documentTexts);
            EmbeddingResponseValidator.Validate(documentEmbeddings, projects.Count, settings.Model);
            var indexedProjects = new List<IndexedProject>();
            for (int index = 0; index < projects.Count; index++)
                indexedProjects.Add(new(projects[index], VectorNormalizer.Normalize(documentEmbeddings[index].Vector.Span)));
            var searchIndex = new SearchIndex(settings.Name, settings.Model, documentEmbeddings[0].Dimensions, indexedProjects);
            SearchIndexValidator.Validate(searchIndex);
            SqliteSearchIndex.Replace(path, searchIndex);
            SearchIndex storedIndex = SqliteSearchIndex.Read(path);
            SearchIndexValidator.Validate(storedIndex);

            GeneratedEmbeddings<Embedding<float>> queryEmbeddings = await ProjectEmbedder.RequestAsync(generator, queryTexts);
            EmbeddingResponseValidator.Validate(queryEmbeddings, 1, settings.Model);
            Embedding<float> queryEmbedding = queryEmbeddings[0];
            SearchIndexValidator.ValidateCompatibility(storedIndex, settings.Name, queryEmbedding.ModelId!, queryEmbedding.Vector.Span);
            List<float> normalizedQuery = VectorNormalizer.Normalize(queryEmbedding.Vector.Span);
            List<SearchResult> semanticResults = SemanticSearch.Rank(storedIndex, normalizedQuery, topK);

            Assert.Equal(5, semanticResults.Count);
            Assert.Equal(5, lexicalResults.Count);
            Assert.Equal(12, storedIndex.Projects.Count);
            Assert.All(semanticResults, result =>
            {
                Assert.InRange(result.Score, -1, 1);
                Assert.False(string.IsNullOrWhiteSpace(result.Snippet));
                Assert.Contains(projects, project => project.Id == result.DocumentId);
            });
        }
        finally { File.Delete(path); }
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateGenerator(HttpClient httpClient) => new AiClientFactory().CreateEmbeddingGenerator(
        new("OpenRouter", "test-key", "test-model", new Uri("https://example.invalid/v1")), httpClient);
}
