namespace ResumeRag;

public static class IndexPreparer
{
    // A document hash change updates metadata; identical passage text can still reuse its embedding.
    public static List<DocumentChunk> FindChanged(List<DocumentChunk> documentChunks, ChunkIndex? previousChunkIndex)
    {
        Dictionary<string, IndexedChunk> existingIndexedChunksByIdDictionary = previousChunkIndex?.IndexedChunks
            .ToDictionary(indexedChunk => indexedChunk.DocumentChunk.ChunkIdString)
            ?? new Dictionary<string, IndexedChunk>();

        return documentChunks.Where(documentChunk =>
            !existingIndexedChunksByIdDictionary.TryGetValue(documentChunk.ChunkIdString, out IndexedChunk? storedIndexedChunk) ||
            storedIndexedChunk.DocumentChunk.TextString != documentChunk.TextString ||
            storedIndexedChunk.DocumentChunk.HeadingString != documentChunk.HeadingString).ToList();
    }

    // Build the desired snapshot in source order; omitted old IDs are removed by either store.
    public static ChunkIndex Merge(IndexMetadata indexMetadata, List<DocumentChunk> documentChunks, ChunkIndex? previousChunkIndex,
        List<IndexedChunk> changedIndexedChunks)
    {
        Dictionary<string, IndexedChunk> availableIndexedChunksByIdDictionary = previousChunkIndex?.IndexedChunks
            .ToDictionary(indexedChunk => indexedChunk.DocumentChunk.ChunkIdString)
            ?? new Dictionary<string, IndexedChunk>();

        foreach (IndexedChunk indexedChunk in changedIndexedChunks)
        {
            availableIndexedChunksByIdDictionary[indexedChunk.DocumentChunk.ChunkIdString] = indexedChunk;
        }

        return new(
            indexMetadata,
            documentChunks.Select(documentChunk => new IndexedChunk
                (documentChunk, availableIndexedChunksByIdDictionary[documentChunk.ChunkIdString].VectorFloats)
            )
            .ToList());
    }
}
