using Microsoft.Extensions.AI;
using Xunit;

public class EmbeddingResponseValidatorTests
{
    // Check every returned model, including a later vector from another model.
    [Fact]
    public void Unexpected_response_model_is_rejected()
    {
        GeneratedEmbeddings<Embedding<float>> embeddings = new(
        [
            new Embedding<float>(new float[] { 1, 0 }) { ModelId = "test-model" },
            new Embedding<float>(new float[] { 0, 1 }) { ModelId = "other-model" }
        ]);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            EmbeddingResponseValidator.Validate(embeddings, 2, "test-model"));

        Assert.Contains("response model", error.Message);
    }

    // Missing vectors must stop the pipeline before indexing or requesting the query embedding.
    [Fact]
    public void Incomplete_response_is_rejected()
    {
        GeneratedEmbeddings<Embedding<float>> embeddings = new(
            [new Embedding<float>(new float[] { 1, 0 }) { ModelId = "test-model" }]);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            EmbeddingResponseValidator.Validate(embeddings, 2, "test-model"));

        Assert.Contains("one vector per input", error.Message);
    }

    // Every document in the same response must use the same embedding dimension.
    [Fact]
    public void Unexpected_vector_dimensions_are_rejected()
    {
        GeneratedEmbeddings<Embedding<float>> embeddings = new(
        [
            new Embedding<float>(new float[] { 1, 0 }) { ModelId = "test-model" },
            new Embedding<float>(new float[] { 0, 1, 0 }) { ModelId = "test-model" }
        ]);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            EmbeddingResponseValidator.Validate(embeddings, 2, "test-model"));

        Assert.Contains("dimensions", error.Message);
    }
}
