using CommunityToolkit.VectorData.SqliteVec;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.VectorData;
using Xunit;

public class SemanticSearchTests
{
    // Handwritten vectors make the closest direction and opposite direction unambiguous.
    [Fact]
    public void Cosine_orders_all_documents_and_respects_top_k()
    {
        // Arrange
        var searchIndex = new SearchIndex(
            "Test",
            "model",
            2,
            [
                new(
                new(
                    "P3",
                    "Opposite project",
                    ["Other"]),
                [-1, 0]),
                new(
                new(
                    "P2",
                    "Perpendicular project",
                    ["Other"]),
                [0, 1]),
                new(
                new(
                    "P1",
                    "Closest project",
                    ["Docker"]),
                [1, 0])
            ]);
        SearchIndexValidator.Validate(searchIndex);
        SearchIndexValidator.ValidateCompatibility(
            searchIndex,
            "Test",
            "model",
            [1, 0]);

        // Act
        List<SearchResult> searchResults = SemanticSearch.Rank(
            searchIndex,
            [1, 0],
            10);

        // Assert
        Assert.Equal(
            new[] { "P1", "P2", "P3" },
            searchResults.Select(searchResult => searchResult.DocumentIdString));
        Assert.Equal(
            new[] { 1d, 0d, -1d },
            searchResults.Select(searchResult => searchResult.ScoreDouble));
        Assert.Equal(
            new[] { 1, 2, 3 },
            searchResults.Select(searchResult => searchResult.RankInt));
        Assert.Single(SemanticSearch.Rank(
            searchIndex,
            [1, 0],
            1));
    }

    // Fake vectors teach the ranking mechanics; they make no claim about a live model's quality.
    [Fact]
    public void Semantic_and_keyword_search_can_choose_different_projects()
    {
        // Arrange
        List<ProjectDescription> projectDescriptions =
        [
            new(
                "P1",
                "Shipped web services using Docker images.",
                ["Docker"]),
            new(
                "P2",
                "Proofread the phrase containerized APIs in a glossary.",
                ["Proofreading"])
        ];
        SearchInputsValidator.Validate(
            projectDescriptions,
            "containerized APIs",
            1);
        var searchIndex = new SearchIndex(
            "Test",
            "model",
            2,
            [
                new(
                projectDescriptions[0],
                [1, 0]),
                new(
                projectDescriptions[1],
                [0, 1])
            ]);
        SearchIndexValidator.Validate(searchIndex);

        SearchIndexValidator.ValidateCompatibility(
            searchIndex,
            "Test",
            "model",
            [1, 0]);

        // Act
        Assert.Equal(
            "P1",
            Assert.Single(SemanticSearch.Rank(
                searchIndex,
                [1, 0],
                1)).DocumentIdString);

        // Assert
        Assert.Equal(
            "P2",
            Assert.Single(LexicalSearch.Rank(
                projectDescriptions,
                "containerized APIs",
                1)).DocumentIdString);
    }

    // A real local connector checks distance conversion and metadata filters without calling an embedding model.
    [Fact]
    public async Task Vector_store_filters_provider_and_model_and_returns_cosine_similarity()
    {
        // Arrange
        string pathString = Path.Combine(
            Path.GetTempPath(),
            $"vector-search-{Guid.NewGuid():N}.sqlite");

        // Act
        // Assert
        try
        {
            var searchIndex = new SearchIndex(
                "Test",
                "model",
                2,
                [
                    new(
                    new(
                        "P3",
                        "Opposite project",
                        ["Other"]),
                    [-1, 0]),
                    new(
                    new(
                        "P2",
                        "Perpendicular project",
                        ["Other"]),
                    [0, 1]),
                    new(
                    new(
                        "P1",
                        "Closest project",
                        ["Docker"]),
                    [1, 0])
                ]);
            SearchIndexValidator.Validate(searchIndex);
            SearchIndexValidator.ValidateCompatibility(
                searchIndex,
                "Test",
                "model",
                [4, 0]);

            var connectionSqliteConnectionStringBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = pathString,
                Pooling = false
            };
            using var sqliteVectorStore = new SqliteVectorStore(connectionSqliteConnectionStringBuilder.ToString());
            var vectorStoreCollectionDefinition = new VectorStoreCollectionDefinition
            {
                Properties =
                [
                    new VectorStoreKeyProperty(
                        nameof(VectorProject.IdString),
                        typeof(string)),
                    new VectorStoreDataProperty(
                        nameof(VectorProject.TextString),
                        typeof(string)),
                    new VectorStoreDataProperty(
                        nameof(VectorProject.ProviderString),
                        typeof(string)),
                    new VectorStoreDataProperty(
                        nameof(VectorProject.ModelString),
                        typeof(string)),
                    new VectorStoreVectorProperty(
                        nameof(VectorProject.VectorReadOnlyMemory),
                        typeof(ReadOnlyMemory<float>),
                        2)
                    {
                        DistanceFunction = DistanceFunction.CosineDistance
                    }
                ]
            };
            using VectorStoreCollection<string, VectorProject> vectorStoreCollection =
                sqliteVectorStore.GetCollection<string, VectorProject>(
                    "projects",
                    vectorStoreCollectionDefinition);
            await vectorStoreCollection.EnsureCollectionDeletedAsync();
            await vectorStoreCollection.EnsureCollectionExistsAsync();

            var vectorProjects = new List<VectorProject>();
            foreach (IndexedProject indexedProject in searchIndex.IndexedProjects)
            {
                vectorProjects.Add(new VectorProject
                {
                    IdString = indexedProject.ProjectDescription.IdString,
                    TextString = indexedProject.ProjectDescription.TextString,
                    ProviderString = searchIndex.ProviderString,
                    ModelString = searchIndex.ModelString,
                    VectorReadOnlyMemory = indexedProject.VectorFloats.ToArray()
                });
            }

            // These vectors point at the query but belong to a different embedding space and must be excluded.
            vectorProjects.Add(new VectorProject
            {
                IdString = "WrongProvider",
                TextString = "Other provider",
                ProviderString = "Other",
                ModelString = "model",
                VectorReadOnlyMemory = new float[] { 1, 0 }
            });
            vectorProjects.Add(new VectorProject
            {
                IdString = "WrongModel",
                TextString = "Other model",
                ProviderString = "Test",
                ModelString = "other-model",
                VectorReadOnlyMemory = new float[] { 1, 0 }
            });
            await vectorStoreCollection.UpsertAsync(vectorProjects);

            // Cosine distance also accepts a query whose length is not one.
            List<SearchResult> searchResults = await SemanticSearch.RankAsync(
                vectorStoreCollection,
                searchIndex,
                new float[] { 4, 0 },
                10);

            Assert.Equal(
                new[] { "P1", "P2", "P3" },
                searchResults.Select(searchResult => searchResult.DocumentIdString));
            Assert.Equal(
                new[] { 1d, 0d, -1d },
                searchResults.Select(searchResult => searchResult.ScoreDouble));
            Assert.Equal(
                new[] { 1, 2, 3 },
                searchResults.Select(searchResult => searchResult.RankInt));
            Assert.Equal(
                "P1",
                Assert.Single(await SemanticSearch.RankAsync(
                    vectorStoreCollection,
                    searchIndex,
                    new float[] { 4, 0 },
                    1)).DocumentIdString);
        }
        finally
        {
            File.Delete(pathString);
        }
    }
}
