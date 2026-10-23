using CommunityToolkit.VectorData.SqliteVec;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.VectorData;
using Xunit;
using VectorProject = SemanticSearch.VectorProject;

public class SemanticSearchTests
{
    // Handwritten vectors make the closest direction and opposite direction unambiguous.
    [Fact]
    public void Cosine_orders_all_documents_and_respects_top_k()
    {
        var searchIndex = new SearchIndex("Test", "model", 2,
        [
            new(new("P3", "Opposite project", ["Other"]), [-1, 0]),
            new(new("P2", "Perpendicular project", ["Other"]), [0, 1]),
            new(new("P1", "Closest project", ["Docker"]), [1, 0])
        ]);
        SearchIndexValidator.Validate(searchIndex);
        SearchIndexValidator.ValidateCompatibility(searchIndex, "Test", "model", [1, 0]);

        List<SearchResult> results = SemanticSearch.Rank(searchIndex, [1, 0], 10);

        Assert.Equal(new[] { "P1", "P2", "P3" }, results.Select(result => result.DocumentId));
        Assert.Equal(new[] { 1d, 0d, -1d }, results.Select(result => result.Score));
        Assert.Equal(new[] { 1, 2, 3 }, results.Select(result => result.Rank));
        Assert.Single(SemanticSearch.Rank(searchIndex, [1, 0], 1));
    }

    // Fake vectors teach the ranking mechanics; they make no claim about a live model's quality.
    [Fact]
    public void Semantic_and_keyword_search_can_choose_different_projects()
    {
        List<ProjectDescription> projects =
        [
            new("P1", "Shipped web services using Docker images.", ["Docker"]),
            new("P2", "Proofread the phrase containerized APIs in a glossary.", ["Proofreading"])
        ];
        SearchInputsValidator.Validate(projects, "containerized APIs", 1);
        var searchIndex = new SearchIndex("Test", "model", 2,
            [new(projects[0], [1, 0]), new(projects[1], [0, 1])]);
        SearchIndexValidator.Validate(searchIndex);
        SearchIndexValidator.ValidateCompatibility(searchIndex, "Test", "model", [1, 0]);

        Assert.Equal("P1", Assert.Single(SemanticSearch.Rank(searchIndex, [1, 0], 1)).DocumentId);
        Assert.Equal("P2", Assert.Single(LexicalSearch.Rank(projects, "containerized APIs", 1)).DocumentId);
    }

    // A real local connector checks distance conversion and metadata filters without calling an embedding model.
    [Fact]
    public async Task Vector_store_filters_provider_and_model_and_returns_cosine_similarity()
    {
        string path = Path.Combine(Path.GetTempPath(), $"vector-search-{Guid.NewGuid():N}.sqlite");
        try
        {
            var searchIndex = new SearchIndex("Test", "model", 2,
            [
                new(new("P3", "Opposite project", ["Other"]), [-1, 0]),
                new(new("P2", "Perpendicular project", ["Other"]), [0, 1]),
                new(new("P1", "Closest project", ["Docker"]), [1, 0])
            ]);
            SearchIndexValidator.Validate(searchIndex);
            SearchIndexValidator.ValidateCompatibility(searchIndex, "Test", "model", [4, 0]);

            var connectionSettings = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false };
            using var store = new SqliteVectorStore(connectionSettings.ToString());
            var definition = new VectorStoreCollectionDefinition
            {
                Properties =
                [
                    new VectorStoreKeyProperty(nameof(VectorProject.Id), typeof(string)),
                    new VectorStoreDataProperty(nameof(VectorProject.Text), typeof(string)),
                    new VectorStoreDataProperty(nameof(VectorProject.Provider), typeof(string)),
                    new VectorStoreDataProperty(nameof(VectorProject.Model), typeof(string)),
                    new VectorStoreVectorProperty(nameof(VectorProject.Vector), typeof(ReadOnlyMemory<float>), 2)
                    {
                        DistanceFunction = DistanceFunction.CosineDistance
                    }
                ]
            };
            using VectorStoreCollection<string, VectorProject> collection =
                store.GetCollection<string, VectorProject>("projects", definition);
            await collection.EnsureCollectionDeletedAsync();
            await collection.EnsureCollectionExistsAsync();

            var records = new List<VectorProject>();
            foreach (IndexedProject indexedProject in searchIndex.Projects)
            {
                records.Add(new VectorProject
                {
                    Id = indexedProject.Project.Id,
                    Text = indexedProject.Project.Text,
                    Provider = searchIndex.Provider,
                    Model = searchIndex.Model,
                    Vector = indexedProject.Vector.ToArray()
                });
            }

            // These vectors point at the query but belong to a different embedding space and must be excluded.
            records.Add(new VectorProject
            {
                Id = "WrongProvider",
                Text = "Other provider",
                Provider = "Other",
                Model = "model",
                Vector = new float[] { 1, 0 }
            });
            records.Add(new VectorProject
            {
                Id = "WrongModel",
                Text = "Other model",
                Provider = "Test",
                Model = "other-model",
                Vector = new float[] { 1, 0 }
            });
            await collection.UpsertAsync(records);

            // Cosine distance also accepts a query whose length is not one.
            List<SearchResult> results = await SemanticSearch.RankAsync(collection, searchIndex, new float[] { 4, 0 }, 10);

            Assert.Equal(new[] { "P1", "P2", "P3" }, results.Select(result => result.DocumentId));
            Assert.Equal(new[] { 1d, 0d, -1d }, results.Select(result => result.Score));
            Assert.Equal(new[] { 1, 2, 3 }, results.Select(result => result.Rank));
            Assert.Equal("P1", Assert.Single(
                await SemanticSearch.RankAsync(collection, searchIndex, new float[] { 4, 0 }, 1)).DocumentId);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
