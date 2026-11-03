namespace ResumeRag.Stores;

// Only storage changes between implementations; chunking, model requests, and evidence rules stay in the lesson.
public interface IChunkStore : IDisposable
{
    string DescriptionString
    {
        get;
    }
    Task<ChunkIndex?> ReadAsync(CancellationToken cancellationToken = default);
    Task SynchronizeAsync(ChunkIndex chunkIndex, CancellationToken cancellationToken = default);
    Task<List<RetrievedChunk>> SearchAsync(List<float> queryFloats, int topKInt, CancellationToken cancellationToken = default);
}
