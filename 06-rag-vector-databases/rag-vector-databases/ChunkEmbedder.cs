using Microsoft.Extensions.AI;

namespace ResumeRag;

public static class ChunkEmbedder
{
    // One native batch call has one deadline covering HTTP attempts and waits.
    public static async Task<GeneratedEmbeddings<Embedding<float>>> RequestAsync(
        IEmbeddingGenerator<string, Embedding<float>> iEmbeddingGenerator, List<string> textsStrings, CancellationToken cancellationToken = default)
    {
        using var deadlineCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadlineCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(AiClientFactory.RequestTimeoutSeconds));
        return await iEmbeddingGenerator.GenerateAsync(textsStrings, cancellationToken: deadlineCancellationTokenSource.Token);
    }
}
