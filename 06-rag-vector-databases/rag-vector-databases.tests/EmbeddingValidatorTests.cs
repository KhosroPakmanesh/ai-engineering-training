using Microsoft.Extensions.AI;
using Xunit;

namespace ResumeRag.Tests;

public class EmbeddingValidatorTests
{
    // A response can have the right count and still belong to the wrong model or vector space.
    [Fact]
    public void Embedding_checks_reject_wrong_count_model_and_dimensions()
    {
        // Arrange
        var generatedEmbeddings = new GeneratedEmbeddings<Embedding<float>>(
        [
            new(new float[]
            {
                1,
                0
            })
            {
                ModelId = "test-model"
            }
        ]);

        // Act
        EmbeddingValidator.Validate(
            generatedEmbeddings,
            1,
            "test-model");

        // Assert
        Assert.Throws<InvalidDataException>(() => EmbeddingValidator.Validate(
            generatedEmbeddings,
            2,
            "test-model"));

        Assert.Throws<InvalidDataException>(() => EmbeddingValidator.Validate(
            generatedEmbeddings,
            1,
            "other-model"));
        Assert.Throws<InvalidDataException>(() => EmbeddingValidator.Validate(
            generatedEmbeddings,
            1,
            "test-model",
            3));
    }
}
