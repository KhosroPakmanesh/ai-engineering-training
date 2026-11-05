using Xunit;

public class VectorNormalizerTests
{
    // A 3-4-5 triangle gives a known unit vector without calculating the expectation using production code.
    [Fact]
    public void Normalization_changes_length_without_changing_direction()
    {
        // Arrange
        VectorValidator.Validate(
            [3, 4],
            2);

        // Act
        List<float> normalizedVectorFloats = VectorNormalizer.Normalize([3, 4]);

        // Assert
        Assert.Equal(
            0.6f,
            normalizedVectorFloats[0],
            6);
        Assert.Equal(
            0.8f,
            normalizedVectorFloats[1],
            6);
    }
}
