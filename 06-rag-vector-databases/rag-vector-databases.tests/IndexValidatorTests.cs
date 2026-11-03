using ResumeRag.Tests.TestData;
using Xunit;

namespace ResumeRag.Tests;

public class IndexValidatorTests
{
    // Equal vector sizes cannot make embeddings from a different model compatible.
    [Fact]
    public void Compatibility_rejects_model_and_dimension_mismatches()
    {
        ChunkIndex chunkIndex = RagFixtures.CreateIndex();
        IndexValidator.Validate(chunkIndex);
        Assert.Contains(
            "embedding model",
            Assert.Throws<InvalidDataException>(() =>
            IndexValidator.ValidateCompatibility(chunkIndex, "OpenRouter", "different-model", 650, 80)).Message);
        List<float> wrongDimensionsFloats = [1, 0, 0];
        List<float> nonfiniteVectorFloats = [float.NaN, 1];
        List<float> zeroVectorFloats = [0, 0];
        Assert.Throws<InvalidDataException>(() => IndexValidator.ValidateVector(wrongDimensionsFloats, 2));
        Assert.Throws<InvalidDataException>(() => IndexValidator.ValidateVector(nonfiniteVectorFloats, 2));
        Assert.Throws<InvalidDataException>(() => IndexValidator.ValidateVector(zeroVectorFloats, 2));
    }
}
