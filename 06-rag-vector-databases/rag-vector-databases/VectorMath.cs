namespace ResumeRag;

public static class VectorMath
{
    // A cosine score compares directions; normalize accepted vectors once before storing or searching.
    public static List<float> Normalize(ReadOnlySpan<float> vectorReadOnlySpan)
    {
        double lengthDouble = 0;
        foreach (float valueFloat in vectorReadOnlySpan)
        {
            lengthDouble += (double)valueFloat * valueFloat;
        }

        lengthDouble = Math.Sqrt(lengthDouble);
        var normalizedFloats = new List<float>(vectorReadOnlySpan.Length);
        foreach (float valueFloat in vectorReadOnlySpan)
        {
            normalizedFloats.Add((float)(valueFloat / lengthDouble));
        }

        return normalizedFloats;
    }

    // Both inputs have unit length, so their dot product is cosine similarity.
    public static double Cosine(
        List<float> leftFloats,
        List<float> rightFloats)
    {
        if (leftFloats.Count != rightFloats.Count)
        {
            throw new InvalidDataException("Vector dimensions do not match.");
        }

        double scoreDouble = 0;
        for (int indexInt = 0; indexInt < leftFloats.Count; indexInt++)
        {
            scoreDouble += (double)leftFloats[indexInt] * rightFloats[indexInt];
        }

        return Math.Clamp(
            scoreDouble,
            -1,
            1);
    }
}
