using Microsoft.Extensions.AI;

public sealed class AiClientFactory
{
    private readonly Dictionary<string, IAiProviderStrategy> _iAiProviderStrategiesDictionary;
    public const int RequestTimeoutSeconds = 120;
    public const int MaxRetries = AiRetryPolicy.MaxRetries;

    // Construct only lightweight strategies here; unused providers need no client or credentials.
    public AiClientFactory()
    {
        List<IAiProviderStrategy> registeredIAiProviderStrategies =
        [
            new OpenRouterProvider(),
            new GeminiProvider()
        ];
        _iAiProviderStrategiesDictionary = new(StringComparer.OrdinalIgnoreCase);
        foreach (IAiProviderStrategy iAiProviderStrategy in registeredIAiProviderStrategies)
        {
            _iAiProviderStrategiesDictionary.Add(
                iAiProviderStrategy.NameString,
                iAiProviderStrategy);
        }
    }

    // Keep selection here while each strategy owns its connection and provider-specific behavior.
    public IChatClient CreateClient(
        ProviderSettings providerSettings,
        HttpClient? httpClient = null)
    {
        return SelectProvider(providerSettings.NameString).CreateClient(
            providerSettings,
            httpClient);
    }

    // Lessons use Microsoft's common embedding interface while provider SDK details stay internal.
    public IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(
        ProviderSettings providerSettings,
        HttpClient? httpClient = null)
    {
        return SelectProvider(providerSettings.NameString).CreateEmbeddingGenerator(
            providerSettings,
            httpClient);
    }

    // Configuration and client creation use the same registrations and supported provider names.
    internal IAiProviderStrategy SelectProvider(string nameString)
    {
        if (!_iAiProviderStrategiesDictionary.TryGetValue(
            nameString,
            out IAiProviderStrategy? selectedIAiProviderStrategy))
        {
            string supportedProvidersString = string.Join(
                " or ",
                _iAiProviderStrategiesDictionary.Keys.Select(providerNameString =>
                providerNameString.ToLowerInvariant()));

            throw new InvalidOperationException(
                $"Set AI_PROVIDER to {supportedProvidersString} in shared/shared/.env, then rebuild. No requests were made.");
        }

        return selectedIAiProviderStrategy;
    }
}
