using System.Runtime.InteropServices;

namespace ResumeRag;

public static class IndexValidator
{
    // Stored data crosses a trust boundary; check it before reusing vectors or accepting search results.
    public static void Validate(ChunkIndex chunkIndex)
    {
        ValidateMetadata(chunkIndex);
        ValidateChunks(chunkIndex);
        ValidateNormalizedVectors(chunkIndex);
    }

    // Missing model metadata or an unusable chunk count makes later vector checks meaningless.
    private static void ValidateMetadata(ChunkIndex chunkIndex)
    {
        if (chunkIndex.IndexMetadata.DimensionsInt <= 0 ||
            string.IsNullOrWhiteSpace(chunkIndex.IndexMetadata.ProviderString) ||
            string.IsNullOrWhiteSpace(chunkIndex.IndexMetadata.EmbeddingModelString) ||
            chunkIndex.IndexedChunks.Count is < 1 or > 500)
        {
            throw new InvalidDataException("The index has invalid metadata or chunk count.");
        }
    }

    // Stable source IDs identify each nonempty passage exactly once.
    private static void ValidateChunks(ChunkIndex chunkIndex)
    {
        var chunkIdStringsHashSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (IndexedChunk indexedChunk in chunkIndex.IndexedChunks)
        {
            if (!chunkIdStringsHashSet.Add(indexedChunk.DocumentChunk.ChunkIdString) ||
                string.IsNullOrWhiteSpace(indexedChunk.DocumentChunk.TextString) ||
                !indexedChunk.DocumentChunk.ChunkIdString.StartsWith(indexedChunk.DocumentChunk.FilenameString + "#chunk-", StringComparison.Ordinal))
            {
                throw new InvalidDataException("The index contains invalid or duplicate chunk IDs or empty text.");
            }
        }
    }

    // Only finite vectors with the stored dimension and unit length can reach retrieval.
    private static void ValidateNormalizedVectors(ChunkIndex chunkIndex)
    {
        foreach (IndexedChunk indexedChunk in chunkIndex.IndexedChunks)
        {
            ValidateVector(indexedChunk.VectorFloats, chunkIndex.IndexMetadata.DimensionsInt);
            double squaredLengthDouble = indexedChunk.VectorFloats.Sum(valueFloat => (double)valueFloat * valueFloat);
            if (Math.Abs(squaredLengthDouble - 1) > 0.001)
            {
                throw new InvalidDataException("Stored vectors must be normalized before indexing.");
            }
        }
    }

    // Model and chunking changes require an explicit new index, even when vector sizes happen to match.
    public static void ValidateCompatibility(ChunkIndex chunkIndex, string providerString, string modelString, int chunkSizeInt, int overlapInt)
    {
        IndexMetadata indexMetadata = chunkIndex.IndexMetadata;
        if (indexMetadata.ProviderString != providerString ||
            indexMetadata.EmbeddingModelString != modelString ||
            indexMetadata.ChunkSizeInt != chunkSizeInt ||
            indexMetadata.OverlapInt != overlapInt ||
            indexMetadata.FormatVersionInt != 1)
        {
            throw new InvalidDataException(
                "Index provider, embedding model, or chunking settings differ. " +
                "Use a new SQLite path or Qdrant collection, or deliberately remove the old lesson index and rerun.");
        }
    }

    // Read the list's existing memory so checking a stored vector needs no second copy.
    public static void ValidateVector(List<float> vectorFloats, int dimensionsInt)
    {
        ValidateVector(CollectionsMarshal.AsSpan(vectorFloats), dimensionsInt);
    }

    // Check stored and provider vectors with the same rules, without changing their values.
    public static void ValidateVector(ReadOnlySpan<float> vectorReadOnlySpan, int dimensionsInt)
    {
        if (dimensionsInt <= 0 || vectorReadOnlySpan.Length != dimensionsInt)
        {
            throw new InvalidDataException("Vector dimension mismatch or zero vector.");
        }

        double squaredLengthDouble = 0;
        foreach (float valueFloat in vectorReadOnlySpan)
        {
            if (!float.IsFinite(valueFloat))
            {
                throw new InvalidDataException("Vectors must contain finite numbers.");
            }

            squaredLengthDouble += (double)valueFloat * valueFloat;
        }

        if (squaredLengthDouble == 0)
        {
            throw new InvalidDataException("Vector dimension mismatch or zero vector.");
        }
    }
}
