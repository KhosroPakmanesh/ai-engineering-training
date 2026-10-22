using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

internal static class OpenAiCompatibleClient
{
    // Both current providers use the same SDK transport, retries, and Chat Completions format.
    public static IChatClient CreateClient(ProviderSettings settings, HttpClient? httpClient,
        Action<ChatCompletionOptions>? configureOptions = null)
    {
        var client = new ChatClient(settings.Model, new ApiKeyCredential(settings.Key), CreateOptions(settings, httpClient));
        return new CompatibleChatClient(client.AsIChatClient(), configureOptions);
    }

    // Check and order wire indexes before Microsoft's adapter converts the response into common embedding values.
    public static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(ProviderSettings settings, HttpClient? httpClient,
        PipelinePolicy? responsePolicy = null)
    {
        OpenAIClientOptions options = CreateOptions(settings, httpClient);
        options.AddPolicy(new EmbeddingResponseOrderPolicy(), PipelinePosition.BeforeTransport);
        // Response policies run in reverse order, so provider conventions are handled before index checks.
        if (responsePolicy is not null) options.AddPolicy(responsePolicy, PipelinePosition.BeforeTransport);
        var client = new EmbeddingClient(settings.Model, new ApiKeyCredential(settings.Key), options);
        return client.AsIEmbeddingGenerator();
    }

    // Chat and embeddings have the same transport, retry limit, and network timeout.
    private static OpenAIClientOptions CreateOptions(ProviderSettings settings, HttpClient? httpClient)
    {
        OpenAIClientOptions clientOptions = new()
        {
            Endpoint = settings.Endpoint,
            RetryPolicy = new AiRetryPolicy(),
            NetworkTimeout = TimeSpan.FromSeconds(AiClientFactory.RequestTimeoutSeconds)
        };
        if (httpClient is not null)
            clientOptions.Transport = new HttpClientPipelineTransport(httpClient);

        return clientOptions;
    }

    // Keep shared wire-format handling beneath the framework's function loop.
    private sealed class CompatibleChatClient(IChatClient innerClient,
        Action<ChatCompletionOptions>? configureOptions) : DelegatingChatClient(innerClient)
    {
        // Each model turn gets fresh SDK options without changing the lesson's ChatOptions.
        public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ChatOptions? requestOptions = options;
            if (options is not null && (options.ResponseFormat is ChatResponseFormatJson || options.Tools?.Count > 0))
            {
                requestOptions = options.Clone();
                requestOptions.RawRepresentationFactory = client =>
                {
                    var rawOptions = options.RawRepresentationFactory?.Invoke(client) as ChatCompletionOptions
                        ?? new ChatCompletionOptions();
                    configureOptions?.Invoke(rawOptions);
                    return rawOptions;
                };
            }

            ChatResponse response = await base.GetResponseAsync(messages, requestOptions, cancellationToken);
            // Both direct Gemini and Gemini behind OpenRouter can return opaque tool metadata.
            if (response.RawRepresentation is ChatCompletion completion && completion.ToolCalls.Count > 0)
            {
                foreach (ChatMessage message in response.Messages.Where(message => message.Role == ChatRole.Assistant))
                    message.RawRepresentation = new AssistantChatMessage(completion);
            }
            return response;
        }
    }
}
