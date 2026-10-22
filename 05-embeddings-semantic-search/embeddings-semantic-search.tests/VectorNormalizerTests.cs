using Xunit;

public class VectorNormalizerTests
{
    // A 3-4-5 triangle gives a known unit vector without calculating the expectation using production code.
    [Fact]
    public void Normalization_changes_length_without_changing_direction()
    {
        VectorValidator.Validate([3, 4], 2);
        List<float> normalizedVector = VectorNormalizer.Normalize([3, 4]);

        Assert.Equal(0.6f, normalizedVector[0], 6);
        Assert.Equal(0.8f, normalizedVector[1], 6);
    }
}
