using Microsoft.Extensions.AI;

public static class EmbeddingResponseValidator
{
    // Wire indexes are already checked in shared plumbing; the lesson owns count, model, and vector requirements.
    public static void Validate(GeneratedEmbeddings<Embedding<float>> embeddings, int inputCount, string expectedModel)
    {
        if (embeddings.Count != inputCount)
            throw new InvalidDataException("The embedding response must contain exactly one vector per input.");

        int dimensions = embeddings[0].Dimensions;
        foreach (Embedding<float> embedding in embeddings)
        {
            if (!string.Equals(embedding.ModelId, expectedModel, StringComparison.Ordinal))
                throw new InvalidDataException($"The embedding response model must be '{expectedModel}'.");
            VectorValidator.Validate(embedding.Vector.Span, dimensions);
        }
    }
}
