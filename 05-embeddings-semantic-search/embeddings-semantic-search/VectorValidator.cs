public static class VectorValidator
{
    // Zero, nonfinite, or differently sized vectors cannot be used for this cosine comparison.
    public static void Validate(ReadOnlySpan<float> vector, int expectedDimensions)
    {
        if (expectedDimensions <= 0 || vector.Length != expectedDimensions)
            throw new InvalidDataException("Embedding vector dimensions do not match.");

        double squaredLength = 0;
        foreach (float value in vector)
        {
            if (!float.IsFinite(value)) throw new InvalidDataException("Embedding vectors must contain only finite numbers.");
            squaredLength += (double)value * value;
        }

        if (squaredLength == 0) throw new InvalidDataException("An embedding vector must not be a zero vector.");
    }
}
