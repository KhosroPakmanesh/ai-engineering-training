public static class VectorValidator
{
    // Zero, nonfinite, or differently sized vectors cannot be used for this cosine comparison.
    public static void Validate(ReadOnlySpan<float> vectorReadOnlySpan, int expectedDimensionsInt)
    {
        if (expectedDimensionsInt <= 0 || vectorReadOnlySpan.Length != expectedDimensionsInt)
        {
            throw new InvalidDataException("Embedding vector dimensions do not match.");
        }

        double squaredLengthDouble = 0;
        foreach (float valueFloat in vectorReadOnlySpan)
        {
            if (!float.IsFinite(valueFloat))
            {
                throw new InvalidDataException("Embedding vectors must contain only finite numbers.");
            }

            squaredLengthDouble += (double)valueFloat * valueFloat;
        }

        if (squaredLengthDouble == 0)
        {
            throw new InvalidDataException("An embedding vector must not be a zero vector.");
        }
    }
}
