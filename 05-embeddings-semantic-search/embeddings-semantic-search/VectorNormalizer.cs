public static class VectorNormalizer
{
    // Give a validated vector length one so a dot product measures cosine similarity.
    public static List<float> Normalize(ReadOnlySpan<float> vectorReadOnlySpan)
    {
        // We show the length calculation and division separately to explain normalization.
        // Recheck numerical limits when replacing these double intermediates with float operations.
        double squaredLengthDouble = 0;
        foreach (float valueFloat in vectorReadOnlySpan)
        {
            squaredLengthDouble += (double)valueFloat * valueFloat;
        }

        double lengthDouble = Math.Sqrt(squaredLengthDouble);
        var normalizedVectorFloats = new List<float>(vectorReadOnlySpan.Length);
        foreach (float valueFloat in vectorReadOnlySpan)
        {
            normalizedVectorFloats.Add((float)(valueFloat / lengthDouble));
        }

        return normalizedVectorFloats;

        // Microsoft alternative, using System.Numerics.Tensors:
        //float lengthFloat = TensorPrimitives.Norm(vectorReadOnlySpan);
        //float[] normalizedFloats = new float[vectorReadOnlySpan.Length];
        //TensorPrimitives.Divide(vectorReadOnlySpan, lengthFloat, normalizedFloats);
        //return normalizedFloats.ToList();
    }
}
