using Xunit;

public class SearchIndexValidatorTests
{
    // A query from another model is incompatible even when its vector has the same size.
    [Theory]
    [InlineData("Test", "other-model")]
    [InlineData("Other", "model")]
    public void Different_models_or_providers_are_rejected(string provider, string model)
    {
        var searchIndex = new SearchIndex("Test", "model", 2, [new(new("P1", "Docker service", ["Docker"]), [1, 0])]);
        SearchIndexValidator.Validate(searchIndex);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            SearchIndexValidator.ValidateCompatibility(searchIndex, provider, model, [1, 0]));

        Assert.Contains("same provider and embedding model", error.Message);
    }

    // A valid query response can still have a different size from the saved collection.
    [Fact]
    public void Different_query_dimensions_are_rejected()
    {
        var searchIndex = new SearchIndex("Test", "model", 2, [new(new("P1", "Docker service", ["Docker"]), [1, 0])]);
        SearchIndexValidator.Validate(searchIndex);
        VectorValidator.Validate([1, 0, 0], 3);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            SearchIndexValidator.ValidateCompatibility(searchIndex, "Test", "model", [1, 0, 0]));

        Assert.Contains("query vector dimensions", error.Message);
    }

    // Stored dimension metadata must agree with every vector.
    [Fact]
    public void Corrupt_stored_dimensions_are_rejected()
    {
        var searchIndex = new SearchIndex("Test", "model", 3, [new(new("P1", "Docker service", ["Docker"]), [1, 0])]);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => SearchIndexValidator.Validate(searchIndex));
        Assert.Contains("dimensions", error.Message);
    }

    // Dot-product ranking requires the stored vectors to have length one.
    [Fact]
    public void Unnormalized_stored_vectors_are_rejected()
    {
        var searchIndex = new SearchIndex("Test", "model", 2, [new(new("P1", "Docker service", ["Docker"]), [3, 4])]);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => SearchIndexValidator.Validate(searchIndex));
        Assert.Contains("normalized", error.Message);
    }
}
