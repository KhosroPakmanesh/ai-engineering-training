using Xunit;

namespace ResumeRag.Tests;

public class VectorMathTests
{
    // Known directions give exact expectations independent of a remote model's quality.
    [Fact]
    public void Normalization_and_cosine_match_known_vectors()
    {
        // Act
        Assert.Equal(
            new List<float> { 0.6f, 0.8f },
            VectorMath.Normalize([3f, 4]));

        // Assert
        Assert.Equal(
            1,
            VectorMath.Cosine(
                [1, 0],
                [1, 0]));
        Assert.Equal(
            0,
            VectorMath.Cosine(
                [1, 0],
                [0, 1]));
        Assert.Equal(
            -1,
            VectorMath.Cosine(
                [1, 0],
                [-1, 0]));
    }
}
