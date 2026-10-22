public static class VectorNormalizer
{
    // Give a validated vector length one so a dot product measures cosine similarity.
    public static List<float> Normalize(ReadOnlySpan<float> vector)
    {
        double squaredLength = 0;
        foreach (float value in vector) squaredLength += (double)value * value;
        double length = Math.Sqrt(squaredLength);
        var normalizedVector = new List<float>(vector.Length);
        foreach (float value in vector) normalizedVector.Add((float)(value / length));
        return normalizedVector;
    }
}
