public static class VectorNormalizer
{
    // Give a validated vector length one so a dot product measures cosine similarity.
    public static List<float> Normalize(ReadOnlySpan<float> vector)
    {
        // We show the length calculation and division separately to explain normalization.
        // Recheck numerical limits when replacing these double intermediates with float operations.
        double squaredLength = 0;
        foreach (float value in vector) squaredLength += (double)value * value;
        double length = Math.Sqrt(squaredLength);
        var normalizedVector = new List<float>(vector.Length);
        foreach (float value in vector) normalizedVector.Add((float)(value / length));
        return normalizedVector;

        // Microsoft alternative, using System.Numerics.Tensors:
        //float length = TensorPrimitives.Norm(vector);
        //float[] normalizedVector = new float[vector.Length];
        //TensorPrimitives.Divide(vector, length, normalizedVector);
        //return normalizedVector.ToList();
    }
}
