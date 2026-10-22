using Microsoft.Extensions.AI;

public static class ProjectEmbedder
{
    // The caller prepares all texts first. One framework call embeds the whole batch, with no lesson retry loop.
    public static async Task<GeneratedEmbeddings<Embedding<float>>> RequestAsync(
        IEmbeddingGenerator<string, Embedding<float>> generator, List<string> texts, CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(AiClientFactory.RequestTimeoutSeconds));
        return await generator.GenerateAsync(texts, cancellationToken: deadline.Token);
    }
}
