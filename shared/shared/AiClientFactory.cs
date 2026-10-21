using Microsoft.Extensions.AI;

public sealed class AiClientFactory
{
    public const int RequestTimeoutSeconds = 120;
    public const int MaxRetries = AiRetryPolicy.MaxRetries;

    private readonly Dictionary<string, IAiProviderStrategy> _providers;

    // Construct only lightweight strategies here; unused providers need no client or credentials.
    public AiClientFactory()
    {
        _providers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["OpenRouter"] = new OpenRouterProvider(),
            ["Gemini"] = new GeminiProvider()
        };
    }

    // Keep selection here while each strategy owns its connection and provider-specific behavior.
    public IChatClient CreateClient(ProviderSettings settings, HttpClient? httpClient = null) =>
        SelectProvider(settings.Name).CreateClient(settings, httpClient);

    // Configuration and client creation use the same registrations and supported provider names.
    internal IAiProviderStrategy SelectProvider(string name)
    {
        if (!_providers.TryGetValue(name, out IAiProviderStrategy? provider))
        {
            string supportedProviders = string.Join(" or ", _providers.Keys.Select(providerName => providerName.ToLowerInvariant()));
            throw new InvalidOperationException(
                $"Set AI_PROVIDER to {supportedProviders} in shared/shared/.env, then rebuild. No requests were made.");
        }
        return provider;
    }
}
