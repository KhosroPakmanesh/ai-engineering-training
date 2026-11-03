using Microsoft.Extensions.AI;

// Provider implementations stay internal; lessons receive Microsoft's chat or embedding interfaces.
internal interface IAiProviderStrategy
{
    string NameString
    {
        get;
    }
    string SettingsPrefixString
    {
        get;
    }
    Uri EndpointUri
    {
        get;
    }
    IChatClient CreateClient(ProviderSettings providerSettings, HttpClient? httpClient);
    IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(ProviderSettings providerSettings, HttpClient? httpClient);
}
