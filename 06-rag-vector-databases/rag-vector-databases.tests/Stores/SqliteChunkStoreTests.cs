using Microsoft.Data.Sqlite;
using ResumeRag.Stores;
using ResumeRag.Tests.TestData;
using Xunit;

namespace ResumeRag.Tests.Stores;

public class SqliteChunkStoreTests
{
    // Repeat ingestion and a changed source keep one row per current source ID and remove stale rows.
    [Fact]
    public async Task Store_upserts_removes_stale_ids_and_ranks_fixed_vectors()
    {
        string pathString = Path.Combine(Path.GetTempPath(), $"rag-{Guid.NewGuid():N}.sqlite");
        try
        {
            using IChunkStore selectedIChunkStore = new SqliteChunkStore(pathString);
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
            chunkIndex.IndexedChunks[0] = chunkIndex.IndexedChunks[0] with { DocumentChunk = chunkIndex.IndexedChunks[0].DocumentChunk with { TextString = "Updated Docker work." } };
            await selectedIChunkStore.SynchronizeAsync(chunkIndex);
            Assert.Equal("Updated Docker work.", Assert.Single((await selectedIChunkStore.ReadAsync())!.IndexedChunks).DocumentChunk.TextString);
        }
        finally { File.Delete(pathString); }
    }

    // Existing indexes keep their original JSON keys when code members gain more explicit names.
    [Fact]
    public async Task Existing_index_JSON_remains_readable_after_member_renames()
    {
        string pathString = Path.Combine(Path.GetTempPath(), $"rag-legacy-{Guid.NewGuid():N}.sqlite");
        const string metadataJsonString = """
            {"Provider":"OpenRouter","EmbeddingModel":"test-embedding","Dimensions":2,"ChunkSize":650,"Overlap":80,"FormatVersion":1}
            """;
        const string documentChunkJsonString = """
            {"ChunkId":"project-a.md#chunk-01","Filename":"project-a.md","Heading":"Delivery","Text":"Mira used Docker.","DocumentHash":"hash-a"}
            """;

        try
        {
            using (var sqliteConnection = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = pathString, Pooling = false }.ToString()))
            {
                await sqliteConnection.OpenAsync();
                using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
                sqliteCommand.CommandText = """
                    CREATE TABLE Metadata (Id INTEGER PRIMARY KEY, Json TEXT NOT NULL);
                    CREATE TABLE Chunks (Id TEXT PRIMARY KEY, ChunkJson TEXT NOT NULL, VectorJson TEXT NOT NULL);
                    INSERT INTO Metadata VALUES (1, $metadata);
                    INSERT INTO Chunks VALUES ('project-a.md#chunk-01', $chunk, '[1,0]');
                    """;
                sqliteCommand.Parameters.AddWithValue("$metadata", metadataJsonString);
                sqliteCommand.Parameters.AddWithValue("$chunk", documentChunkJsonString);
                await sqliteCommand.ExecuteNonQueryAsync();
            }

            using IChunkStore selectedIChunkStore = new SqliteChunkStore(pathString);
            ChunkIndex loadedChunkIndex = Assert.IsType<ChunkIndex>(await selectedIChunkStore.ReadAsync());
            IndexValidator.Validate(loadedChunkIndex);
            IndexValidator.ValidateCompatibility(loadedChunkIndex, "OpenRouter", "test-embedding", 650, 80);
            IndexedChunk indexedChunk = Assert.Single(loadedChunkIndex.IndexedChunks);
            Assert.Equal("project-a.md#chunk-01", indexedChunk.DocumentChunk.ChunkIdString);
            Assert.Equal("Mira used Docker.", indexedChunk.DocumentChunk.TextString);
            Assert.Equal(new List<float> { 1, 0 }, indexedChunk.VectorFloats);

            await selectedIChunkStore.SynchronizeAsync(loadedChunkIndex);
            ChunkIndex rereadChunkIndex = Assert.IsType<ChunkIndex>(await selectedIChunkStore.ReadAsync());
            Assert.Equal(loadedChunkIndex.IndexMetadata, rereadChunkIndex.IndexMetadata);
            Assert.Equal(indexedChunk.DocumentChunk, Assert.Single(rereadChunkIndex.IndexedChunks).DocumentChunk);
        }
        finally { File.Delete(pathString); }
    }
}
