using Qdrant.Client;
using ResumeRag.Stores;
using ResumeRag.Tests.TestData;
using Xunit;

namespace ResumeRag.Tests.Stores;

public class QdrantChunkStoreTests
{
    // This local-server integration costs no AI calls and uses its own collection, removed even on failure.
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Real_server_upserts_removes_stale_ids_and_checks_dimensions()
    {
        string nameString = $"rag-test-{Guid.NewGuid():N}";
        using var cleanupQdrantClient = new QdrantClient("localhost", 6334, grpcTimeout: TimeSpan.FromSeconds(20));
        try
        {
            using IChunkStore selectedIChunkStore = new QdrantChunkStore(
                new QdrantClient("localhost", 6334, grpcTimeout: TimeSpan.FromSeconds(20)), nameString);
            Assert.Null(await selectedIChunkStore.ReadAsync());
            ChunkIndex chunkIndex = RagFixtures.CreateIndex();
            IndexValidator.Validate(chunkIndex);
            await selectedIChunkStore.SynchronizeAsync(chunkIndex);
            await selectedIChunkStore.SynchronizeAsync(chunkIndex);
            Assert.Equal(2, (await selectedIChunkStore.ReadAsync())!.IndexedChunks.Count);
            List<RetrievedChunk> rankedRetrievedChunks = await selectedIChunkStore.SearchAsync([1, 0], 2);
            Assert.Equal("project-a.md#chunk-01", rankedRetrievedChunks[0].DocumentChunk.ChunkIdString);
            Assert.Equal(1, rankedRetrievedChunks[0].ScoreDouble);
            chunkIndex.IndexedChunks.RemoveAt(1);
            chunkIndex.IndexedChunks[0] = chunkIndex.IndexedChunks[0] with
            {
                DocumentChunk = chunkIndex.IndexedChunks[0].DocumentChunk with
                {
                    TextString = "Updated Docker work."
                }
            };
            await selectedIChunkStore.SynchronizeAsync(chunkIndex);
            Assert.Equal("Updated Docker work.",
                Assert.Single((await selectedIChunkStore.ReadAsync())!.IndexedChunks).DocumentChunk.TextString);
            ChunkIndex incompatibleChunkIndex = chunkIndex with
            {
                IndexMetadata = chunkIndex.IndexMetadata with
                {
                    DimensionsInt = 3
                }
            };
            InvalidDataException invalidDataException = await Assert.ThrowsAsync<InvalidDataException>(() => selectedIChunkStore.SynchronizeAsync(incompatibleChunkIndex));
            Assert.Contains("dimension", invalidDataException.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (await cleanupQdrantClient.CollectionExistsAsync(nameString))
            {
                await cleanupQdrantClient.DeleteCollectionAsync(nameString);
            }
        }
    }
}
