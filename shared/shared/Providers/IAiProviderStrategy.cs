using Microsoft.Extensions.AI;

// Provider implementations stay internal; lessons receive Microsoft's chat or embedding interfaces.
internal interface IAiProviderStrategy
{
    string Name { get; }
    string SettingsPrefix { get; }
    Uri Endpoint { get; }
    IChatClient CreateClient(ProviderSettings settings, HttpClient? httpClient);
    IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(ProviderSettings settings, HttpClient? httpClient);
}
