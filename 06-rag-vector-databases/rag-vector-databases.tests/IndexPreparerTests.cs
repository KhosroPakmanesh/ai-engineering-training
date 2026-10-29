using ResumeRag.Tests.TestData;
using Xunit;

namespace ResumeRag.Tests;

public class IndexPreparerTests
{
    // Changed hashes refresh source metadata; unchanged embedding text does not create another paid input.
    [Fact]
    public void Preparation_reuses_unchanged_vectors_updates_metadata_and_removes_missing_ids()
    {
        ChunkIndex previousChunkIndex = RagFixtures.CreateIndex();
        DocumentChunk updatedDocumentChunk = previousChunkIndex.IndexedChunks[0].DocumentChunk with { DocumentHashString = "new-hash" };
        List<DocumentChunk> sourcesDocumentChunks = [updatedDocumentChunk];
        Assert.Empty(IndexPreparer.FindChanged(sourcesDocumentChunks, previousChunkIndex));
        ChunkIndex mergedChunkIndex = IndexPreparer.Merge(previousChunkIndex.IndexMetadata, sourcesDocumentChunks, previousChunkIndex, []);
        Assert.Equal("new-hash", Assert.Single(mergedChunkIndex.IndexedChunks).DocumentChunk.DocumentHashString);
        Assert.Same(previousChunkIndex.IndexedChunks[0].VectorFloats, mergedChunkIndex.IndexedChunks[0].VectorFloats);
        Assert.Single(IndexPreparer.FindChanged([updatedDocumentChunk with { TextString = "Changed Docker work." }], previousChunkIndex));
    }
}
