using Qdrant.Client;

namespace ResumeRag.Stores;

public enum StoreBackend
{
    Sqlite, Qdrant
}

public static class ChunkStoreFactory
{
    // Construct only the selected backend; ordinary runs never create a Qdrant client or contact its server.
    public static IChunkStore Create(
        StoreBackend backendStoreBackend,
        string outputFolderString)
    {
        return backendStoreBackend switch
        {
            StoreBackend.Sqlite => new SqliteChunkStore(Path.Combine(
                outputFolderString,
                "chunks.sqlite")),
            StoreBackend.Qdrant => new QdrantChunkStore(
                new QdrantClient(
                    "localhost",
                    6334,
                    grpcTimeout: TimeSpan.FromSeconds(30)),
                "resume-rag"),
            _ => throw new ArgumentOutOfRangeException(nameof(backendStoreBackend))
        };
    }
}
