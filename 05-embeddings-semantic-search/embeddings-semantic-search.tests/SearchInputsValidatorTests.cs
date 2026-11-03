using Xunit;

public class SearchInputsValidatorTests
{
    // An unusable query must fail before any vector request is prepared.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_query_is_rejected(string queryString)
    {
        Assert.Throws<ArgumentException>(() => SearchInputsValidator.Validate([new("P1", "Docker web services", ["Docker"])], queryString, 5));
    }

    // Top-k describes a positive number of results, even when the corpus is smaller.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Nonpositive_top_k_is_rejected(int topKInt)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SearchInputsValidator.Validate([new("P1", "Docker web services", ["Docker"])], "web services", topKInt));
    }

    // Independent problems near the end of the corpus should all be reported with their field paths.
    [Fact]
    public void Later_project_errors_are_collected_without_repairing_the_inputs()
    {
        List<ProjectDescription> projectDescriptions = [new("P1", "Docker web services", ["Docker"]), new("P1", " ", [])];

        InvalidDataException errorInvalidDataException = Assert.Throws<InvalidDataException>(() => SearchInputsValidator.Validate(projectDescriptions, "web services", 5));

        Assert.Contains("projects[1].id", errorInvalidDataException.Message);
        Assert.Contains("projects[1].text", errorInvalidDataException.Message);
        Assert.Contains("projects[1].skills", errorInvalidDataException.Message);
        Assert.Equal(" ", projectDescriptions[1].TextString);
    }

    // Bound query length before paying for the first batch.
    [Fact]
    public void Oversized_query_is_rejected()
    {
        List<ProjectDescription> projectDescriptions = [new("P1", "Docker web services", ["Docker"])];
        Assert.Throws<ArgumentException>(() => SearchInputsValidator.Validate(projectDescriptions, new string('x', 501), 5));
    }

    // A later oversized project must not allow earlier embeddings to be requested.
    [Fact]
    public void Oversized_project_is_rejected()
    {
        List<ProjectDescription> projectDescriptions = [new("P1", new string('x', 4_001), ["Docker"])];
        InvalidDataException errorInvalidDataException = Assert.Throws<InvalidDataException>(() => SearchInputsValidator.Validate(projectDescriptions, "web services", 5));
        Assert.Contains("projects[0].text", errorInvalidDataException.Message);
    }

    // An empty corpus provides nothing to index or compare.
    [Fact]
    public void Empty_corpus_is_rejected()
    {
        Assert.Throws<InvalidDataException>(() => SearchInputsValidator.Validate([], "web services", 5));
    }

    // Keep the document count within the declared per-run budget.
    [Fact]
    public void Oversized_corpus_is_rejected()
    {
        Assert.Throws<InvalidDataException>(() => SearchInputsValidator.Validate(
            Enumerable.Range(0, 101).Select(indexInt => new ProjectDescription($"P{indexInt}", "Docker web services", ["Docker"])).ToList(),
            "web services", 5));
    }
}
