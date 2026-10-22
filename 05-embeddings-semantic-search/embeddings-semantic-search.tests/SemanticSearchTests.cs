using Xunit;

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
}
