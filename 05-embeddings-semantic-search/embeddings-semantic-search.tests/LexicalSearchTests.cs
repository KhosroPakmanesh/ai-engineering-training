using Xunit;

public class LexicalSearchTests
{
    // Repetition and case do not add points, and equal scores use stable document IDs.
    [Fact]
    public void Keyword_counts_are_distinct_and_ties_are_deterministic()
    {
        List<ProjectDescription> projects =
        [
            new("P2", "Docker, APIs; Docker!", ["Docker"]),
            new("P1", "DOCKER APIs", ["Docker"]),
            new("P3", "A garden spreadsheet", ["Spreadsheets"])
        ];

        List<SearchResult> results = LexicalSearch.Rank(projects, "docker DOCKER apis", 5);
        List<SearchResult> repeatedResults = LexicalSearch.Rank(projects.AsEnumerable().Reverse().ToList(), "docker DOCKER apis", 5);

        Assert.Equal(new[] { "P1", "P2", "P3" }, results.Select(result => result.DocumentId));
        Assert.Equal(new[] { 2d, 2d, 0d }, results.Select(result => result.Score));
        Assert.Equal(results, repeatedResults);
    }

    // Skills are displayed as metadata; neither strategy gets extra search text from them.
    [Fact]
    public void Skill_labels_do_not_add_keyword_matches()
    {
        SearchResult result = Assert.Single(LexicalSearch.Rank([new("P1", "A garden spreadsheet", ["Docker"])], "Docker", 1));
        Assert.Equal(0, result.Score);
    }
}
