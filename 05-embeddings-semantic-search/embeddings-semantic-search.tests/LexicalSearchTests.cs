using Xunit;

public class LexicalSearchTests
{
    // Repetition and case do not add points, and equal scores use stable document IDs.
    [Fact]
    public void Keyword_counts_are_distinct_and_ties_are_deterministic()
    {
        // Arrange
        List<ProjectDescription> projectDescriptions =
        [
            new(
                "P2",
                "Docker, APIs; Docker!",
                ["Docker"]),
            new(
                    "P1",
                    "DOCKER APIs",
                    ["Docker"]),
            new(
                "P3",
                "A garden spreadsheet",
                ["Spreadsheets"])
        ];

        // Act
        List<SearchResult> searchResults = LexicalSearch.Rank(
            projectDescriptions,
            "docker DOCKER apis",
            5);

        List<SearchResult> repeatedSearchResults =
            LexicalSearch.Rank(
                projectDescriptions.AsEnumerable().Reverse().ToList(),
                "docker DOCKER apis",
                5);

        // Assert
        Assert.Equal(
            new[] { "P1", "P2", "P3" },
            searchResults.Select(searchResult => searchResult.DocumentIdString));
        Assert.Equal(
            new[] { 2d, 2d, 0d },
            searchResults.Select(searchResult => searchResult.ScoreDouble));
        Assert.Equal(
            searchResults,
            repeatedSearchResults);
    }

    // Skills are displayed as metadata; neither strategy gets extra search text from them.
    [Fact]
    public void Skill_labels_do_not_add_keyword_matches()
    {
        // Act
        SearchResult searchResult = Assert.Single(LexicalSearch.Rank(
            [
                new(
                "P1",
                "A garden spreadsheet",
                ["Docker"])
            ],
            "Docker",
            1));

        // Assert
        Assert.Equal(
            0,
            searchResult.ScoreDouble);
    }
}
