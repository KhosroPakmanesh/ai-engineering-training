using Xunit;

public class VectorValidatorTests
{
    // Each rejected vector breaks a different requirement of cosine similarity.
    [Theory]
    [InlineData(0f, 0f, "zero")]
    [InlineData(float.NaN, 1f, "finite")]
    [InlineData(float.PositiveInfinity, 1f, "finite")]
    public void Invalid_vector_values_are_rejected(float first, float second, string reason)
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => VectorValidator.Validate([first, second], 2));
        Assert.Contains(reason, error.Message);
    }

    // Different embedding sizes must never be compared by silently ignoring extra numbers.
    [Fact]
    public void Different_dimensions_are_rejected()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => VectorValidator.Validate([1, 0], 3));
        Assert.Contains("dimensions", error.Message);
    }
}
