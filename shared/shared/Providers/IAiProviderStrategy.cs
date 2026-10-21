using Microsoft.Extensions.AI;

// Provider implementations stay internal; lessons receive only the framework's IChatClient.
internal interface IAiProviderStrategy
{
    string Name { get; }
    string SettingsPrefix { get; }
    Uri Endpoint { get; }
    IChatClient CreateClient(ProviderSettings settings, HttpClient? httpClient);
}
