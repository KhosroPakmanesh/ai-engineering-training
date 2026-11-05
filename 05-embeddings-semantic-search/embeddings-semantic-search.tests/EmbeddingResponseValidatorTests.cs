using Microsoft.Extensions.AI;
using Xunit;

public class EmbeddingResponseValidatorTests
{
    // Check every returned model, including a later vector from another model.
    [Fact]
    public void Unexpected_response_model_is_rejected()
    {
        // Arrange
        GeneratedEmbeddings<Embedding<float>> generatedEmbeddings = new(
        [
            new Embedding<float>(new float[] { 1, 0 })
            {
                ModelId = "test-model"
            },
            new Embedding<float>(new float[] { 0, 1 })
            {
                ModelId = "other-model"
            }
        ]);

        // Act
        InvalidDataException errorInvalidDataException = Assert.Throws<InvalidDataException>(() =>
            EmbeddingResponseValidator.Validate(
                generatedEmbeddings,
                2,
                "test-model"));

        // Assert
        Assert.Contains(
            "response model",
            errorInvalidDataException.Message);
    }

    // Missing vectors must stop the pipeline before indexing or requesting the query embedding.
    [Fact]
    public void Incomplete_response_is_rejected()
    {
        // Arrange
        GeneratedEmbeddings<Embedding<float>> generatedEmbeddings = new(
            [
                new Embedding<float>(new float[] { 1, 0 })
                {
                    ModelId = "test-model"
                }
            ]);

        // Act
        InvalidDataException errorInvalidDataException = Assert.Throws<InvalidDataException>(() =>
            EmbeddingResponseValidator.Validate(
                generatedEmbeddings,
                2,
                "test-model"));

        // Assert
        Assert.Contains(
            "one vector per input",
            errorInvalidDataException.Message);
    }

    // Every document in the same response must use the same embedding dimension.
    [Fact]
    public void Unexpected_vector_dimensions_are_rejected()
    {
        // Arrange
        GeneratedEmbeddings<Embedding<float>> generatedEmbeddings = new(
        [
            new Embedding<float>(new float[] { 1, 0 })
            {
                ModelId = "test-model"
            },
            new Embedding<float>(new float[] { 0, 1, 0 })
            {
                ModelId = "test-model"
            }
        ]);

        // Act
        InvalidDataException errorInvalidDataException = Assert.Throws<InvalidDataException>(() =>
            EmbeddingResponseValidator.Validate(
                generatedEmbeddings,
                2,
                "test-model"));

        // Assert
        Assert.Contains(
            "dimensions",
            errorInvalidDataException.Message);
    }
}
