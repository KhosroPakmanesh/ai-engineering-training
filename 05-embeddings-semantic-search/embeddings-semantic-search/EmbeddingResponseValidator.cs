using Microsoft.Extensions.AI;

public static class EmbeddingResponseValidator
{
    // Wire indexes are already checked in shared plumbing; the lesson owns count, model, and vector requirements.
    public static void Validate(
        GeneratedEmbeddings<Embedding<float>> generatedEmbeddings,
        int inputCountInt,
        string expectedModelString)
    {
        if (generatedEmbeddings.Count != inputCountInt)
        {
            throw new InvalidDataException("The embedding response must contain exactly one vector per input.");
        }

        int dimensionsInt = generatedEmbeddings[0].Dimensions;
        foreach (Embedding<float> embedding in generatedEmbeddings)
        {
            if (!string.Equals(
                embedding.ModelId,
                expectedModelString,
                StringComparison.Ordinal))
            {
                throw new InvalidDataException($"The embedding response model must be '{expectedModelString}'.");
            }

            VectorValidator.Validate(
                embedding.Vector.Span,
                dimensionsInt);
        }
    }
}
