using Microsoft.Extensions.AI;

internal sealed class GeminiProvider : IAiProviderStrategy
{
    public string Name => "Gemini";
    public string SettingsPrefix => "GEMINI";
    public Uri Endpoint { get; } = new("https://generativelanguage.googleapis.com/v1beta/openai/");

    // Google's compatibility endpoint needs no OpenRouter routing options.
    public IChatClient CreateClient(ProviderSettings settings, HttpClient? httpClient) =>
        OpenAiCompatibleClient.CreateClient(settings, httpClient);
}
