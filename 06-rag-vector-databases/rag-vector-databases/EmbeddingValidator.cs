using Microsoft.Extensions.AI;

namespace ResumeRag;

public static class EmbeddingValidator
{
    // Shared plumbing checked wire order; the lesson checks count, model, dimensions, and numeric values.
    public static void Validate(
        GeneratedEmbeddings<Embedding<float>> generatedEmbeddings,
        int countInt,
        string modelString,
        int? dimensionsInt = null)
    {
        if (countInt <= 0 || generatedEmbeddings.Count != countInt)
        {
            throw new InvalidDataException("Expected exactly one embedding per input.");
        }

        int expectedDimensionsInt = dimensionsInt ?? generatedEmbeddings[0].Dimensions;
        foreach (Embedding<float> embedding in generatedEmbeddings)
        {
            if (embedding.ModelId != modelString)
            {
                throw new InvalidDataException($"Expected embedding model '{modelString}'.");
            }

            IndexValidator.ValidateVector(
                embedding.Vector.Span,
                expectedDimensionsInt);
        }
    }
}
