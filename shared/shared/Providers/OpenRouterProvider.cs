using Microsoft.Extensions.AI;
using OpenAI.Chat;

internal sealed class OpenRouterProvider : IAiProviderStrategy
{
    public string NameString => "OpenRouter";
    public string SettingsPrefixString => "OPENROUTER";
    public Uri EndpointUri { get; } = new("https://openrouter.ai/api/v1");

    // OpenRouter shares the wire format, but needs its own parameter-routing extension.
    public IChatClient CreateClient(
        ProviderSettings providerSettings,
        HttpClient? httpClient)
    {
        return OpenAiCompatibleClient.CreateClient(
            providerSettings,
            httpClient,
            ConfigureRouting);
    }

    // Embeddings use the same endpoint and credentials, with a separately configured model.
    public IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(
        ProviderSettings providerSettings,
        HttpClient? httpClient)
    {
        return OpenAiCompatibleClient.CreateEmbeddingGenerator(
            providerSettings,
            httpClient);
    }

    // Require support for every requested parameter when the lesson uses schemas or tools.
    private static void ConfigureRouting(ChatCompletionOptions chatCompletionOptions)
    {
#pragma warning disable SCME0001
        chatCompletionOptions.Patch.Set(
            "$.provider"u8,
            """{"require_parameters":true}"""u8);
#pragma warning restore SCME0001
    }
}
