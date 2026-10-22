using Microsoft.Extensions.AI;
using OpenAI.Chat;

internal sealed class OpenRouterProvider : IAiProviderStrategy
{
    public string Name => "OpenRouter";
    public string SettingsPrefix => "OPENROUTER";
    public Uri Endpoint { get; } = new("https://openrouter.ai/api/v1");

    // OpenRouter shares the wire format, but needs its own parameter-routing extension.
    public IChatClient CreateClient(ProviderSettings settings, HttpClient? httpClient) =>
        OpenAiCompatibleClient.CreateClient(settings, httpClient, ConfigureRouting);

    // Embeddings use the same endpoint and credentials, with a separately configured model.
    public IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator
        (ProviderSettings settings, HttpClient? httpClient) =>
        OpenAiCompatibleClient.CreateEmbeddingGenerator(settings, httpClient);

    // Require support for every requested parameter when the lesson uses schemas or tools.
    private static void ConfigureRouting(ChatCompletionOptions options)
    {
#pragma warning disable SCME0001
        options.Patch.Set("$.provider"u8, """{"require_parameters":true}"""u8);
#pragma warning restore SCME0001
    }
}
