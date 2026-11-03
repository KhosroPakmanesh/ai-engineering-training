using Microsoft.Extensions.AI;

public static class ProjectEmbedder
{
    // The caller prepares all texts first. One framework call embeds the whole batch, with no lesson retry loop.
    public static async Task<GeneratedEmbeddings<Embedding<float>>> RequestAsync(
        IEmbeddingGenerator<string, Embedding<float>> iEmbeddingGenerator, List<string> textStrings,
        CancellationToken cancellationToken = default)
    {
        using var deadlineCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadlineCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(AiClientFactory.RequestTimeoutSeconds));
        return await iEmbeddingGenerator.GenerateAsync(textStrings, cancellationToken: deadlineCancellationTokenSource.Token);
    }
}
