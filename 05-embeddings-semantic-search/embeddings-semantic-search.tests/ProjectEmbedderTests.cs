using System.Text.Json;
using Microsoft.Extensions.AI;
using Xunit;

public class ProjectEmbedderTests
{
    // Every project must pass local validation before any request reaches the HTTP boundary.
    [Fact]
    public async Task Invalid_later_project_prevents_all_remote_requests()
    {
        List<ProjectDescription> projectDescriptions = await EmbeddingTestData.LoadAsync();
        projectDescriptions[^1] = projectDescriptions[^1] with
        {
            TextString = " "
        };
        using var embeddingResponseHandler = new EmbeddingResponseHandler(EmbeddingTestData.Response([[1, 0]]));
        using var httpClient = new HttpClient(embeddingResponseHandler);
        using IEmbeddingGenerator<string, Embedding<float>> iEmbeddingGenerator = CreateGenerator(httpClient);

        InvalidDataException errorInvalidDataException = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            SearchInputsValidator.Validate(projectDescriptions, "containerized APIs", 5);
            List<string> textStrings = projectDescriptions.Select(projectDescription => projectDescription.TextString).ToList();
            await ProjectEmbedder.RequestAsync(iEmbeddingGenerator, textStrings);
        });

        Assert.Contains("projects[11].text", errorInvalidDataException.Message);
        Assert.Empty(embeddingResponseHandler.RequestBodyStrings);
    }

    // The native request must preserve the complete prepared batch and pass its deadline token to HTTP.
    [Fact]
    public async Task Batch_request_preserves_original_text_and_cancellation()
    {
        List<string> textStrings = ["Docker service", "Garden budget"];
        using var embeddingResponseHandler = new EmbeddingResponseHandler(EmbeddingTestData.Response([[1, 0], [0, 1]]));
        using var httpClient = new HttpClient(embeddingResponseHandler);

        using IEmbeddingGenerator<string, Embedding<float>> iEmbeddingGenerator = CreateGenerator(httpClient);
        GeneratedEmbeddings<Embedding<float>> responseGeneratedEmbeddings = await ProjectEmbedder.RequestAsync(iEmbeddingGenerator, textStrings);
        EmbeddingResponseValidator.Validate(responseGeneratedEmbeddings, 2, "test-model");

        using JsonDocument requestJsonDocument = JsonDocument.Parse(Assert.Single(embeddingResponseHandler.RequestBodyStrings));
        Assert.Equal(textStrings,
            requestJsonDocument.RootElement.GetProperty("input").EnumerateArray().Select(valueJsonElement => valueJsonElement.GetString()));
        Assert.True(embeddingResponseHandler.HadCancelableTokenBool);
    }

    // Cancellation should stop an active embedding request as well as one canceled before it starts.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Caller_cancellation_stops_native_requests(bool cancelBeforeRequestBool)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        using var embeddingResponseHandler = new EmbeddingResponseHandler(EmbeddingTestData.Response([[1, 0]]), cancellationTokenSource.Cancel);
        using var httpClient = new HttpClient(embeddingResponseHandler);
        using IEmbeddingGenerator<string, Embedding<float>> iEmbeddingGenerator = CreateGenerator(httpClient);
        if (cancelBeforeRequestBool)
        {
            cancellationTokenSource.Cancel();
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ProjectEmbedder.RequestAsync(iEmbeddingGenerator, ["Docker service"], cancellationTokenSource.Token));

        Assert.Equal(cancelBeforeRequestBool ? 0 : 1, embeddingResponseHandler.RequestBodyStrings.Count);
    }

    // Follow the application's complete acceptance flow using the real selected provider and copied corpus.
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Live_corpus_and_query_pass_validation_storage_and_both_rankings()
    {
        const string queryString = "experience building containerized APIs";
        const int topKInt = 5;
        List<ProjectDescription> projectDescriptions = await EmbeddingTestData.LoadAsync();
        SearchInputsValidator.Validate(projectDescriptions, queryString, topKInt);
        List<string> documentTextStrings = projectDescriptions.Select(projectDescription => projectDescription.TextString).ToList();
        List<string> queryTextStrings = [queryString];
        List<SearchResult> lexicalSearchResults = LexicalSearch.Rank(projectDescriptions, queryString, topKInt);
        ProviderSettings providerSettings = await SettingsReader.ReadSettingsAsync(AppContext.BaseDirectory, useEmbeddingModelBool: true);
        using IEmbeddingGenerator<string, Embedding<float>> iEmbeddingGenerator = new AiClientFactory().CreateEmbeddingGenerator(providerSettings);
        string pathString = Path.Combine(Path.GetTempPath(), $"embedding-integration-{Guid.NewGuid():N}.sqlite");
        try
        {
            GeneratedEmbeddings<Embedding<float>> documentGeneratedEmbeddings = await ProjectEmbedder.RequestAsync(iEmbeddingGenerator, documentTextStrings);
            EmbeddingResponseValidator.Validate(documentGeneratedEmbeddings, projectDescriptions.Count, providerSettings.ModelString);
            var indexedProjects = new List<IndexedProject>();
            for (int indexInt = 0; indexInt < projectDescriptions.Count; indexInt++)
            {
                indexedProjects.Add(new(projectDescriptions[indexInt],
                    VectorNormalizer.Normalize(documentGeneratedEmbeddings[indexInt].Vector.Span)));
            }

            var searchIndex = new SearchIndex(providerSettings.NameString, providerSettings.ModelString,
                documentGeneratedEmbeddings[0].Dimensions, indexedProjects);
            SearchIndexValidator.Validate(searchIndex);
            SqliteSearchIndex.Replace(pathString, searchIndex);
            SearchIndex storedSearchIndex = SqliteSearchIndex.Read(pathString);
            SearchIndexValidator.Validate(storedSearchIndex);

            GeneratedEmbeddings<Embedding<float>> queryGeneratedEmbeddings = await ProjectEmbedder.RequestAsync(iEmbeddingGenerator, queryTextStrings);
            EmbeddingResponseValidator.Validate(queryGeneratedEmbeddings, 1, providerSettings.ModelString);
            Embedding<float> queryEmbedding = queryGeneratedEmbeddings[0];
            SearchIndexValidator.ValidateCompatibility(storedSearchIndex, providerSettings.NameString, queryEmbedding.ModelId!,
                queryEmbedding.Vector.Span);
            List<float> normalizedQueryFloats = VectorNormalizer.Normalize(queryEmbedding.Vector.Span);
            List<SearchResult> semanticSearchResults = SemanticSearch.Rank(storedSearchIndex, normalizedQueryFloats, topKInt);

            Assert.Equal(5, semanticSearchResults.Count);
            Assert.Equal(5, lexicalSearchResults.Count);
            Assert.Equal(12, storedSearchIndex.IndexedProjects.Count);
            Assert.All(
                semanticSearchResults,
                searchResult =>
            {
                Assert.InRange(searchResult.ScoreDouble, -1, 1);
                Assert.False(string.IsNullOrWhiteSpace(searchResult.SnippetString));
                Assert.Contains(projectDescriptions, projectDescription => projectDescription.IdString == searchResult.DocumentIdString);
            });
        }
        finally { File.Delete(pathString); }
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateGenerator(HttpClient httpClient)
    {
        return new AiClientFactory().CreateEmbeddingGenerator(
            new("OpenRouter", "test-key", "test-model", new Uri("https://example.invalid/v1")), httpClient);
    }
}
