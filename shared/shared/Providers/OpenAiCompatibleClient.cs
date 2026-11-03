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
    public static IChatClient CreateClient(ProviderSettings providerSettings, HttpClient? httpClient,
        Action<ChatCompletionOptions>? configureOptionsAction = null)
    {
        var chatClient = new ChatClient(providerSettings.ModelString, new ApiKeyCredential(providerSettings.KeyString),
            CreateOptions(providerSettings, httpClient));
        return new CompatibleChatClient(chatClient.AsIChatClient(), configureOptionsAction);
    }

    // Check and order wire indexes before Microsoft's adapter converts the response into common embedding values.
    public static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(ProviderSettings providerSettings,
        HttpClient? httpClient, PipelinePolicy? responsePolicyPipelinePolicy = null)
    {
        OpenAIClientOptions openAIClientOptions = CreateOptions(providerSettings, httpClient);
        openAIClientOptions.AddPolicy(new EmbeddingResponseOrderPolicy(), PipelinePosition.BeforeTransport);
        // Response policies run in reverse order, so provider conventions are handled before index checks.
        if (responsePolicyPipelinePolicy is not null)
        {
            openAIClientOptions.AddPolicy(responsePolicyPipelinePolicy, PipelinePosition.BeforeTransport);
        }

        var embeddingClient = new EmbeddingClient(providerSettings.ModelString, new ApiKeyCredential(providerSettings.KeyString),
            openAIClientOptions);
        return embeddingClient.AsIEmbeddingGenerator();
    }

    // Chat and embeddings have the same transport, retry limit, and network timeout.
    private static OpenAIClientOptions CreateOptions(ProviderSettings providerSettings, HttpClient? httpClient)
    {
        OpenAIClientOptions openAIClientOptions = new()
        {
            Endpoint = providerSettings.EndpointUri,
            RetryPolicy = new AiRetryPolicy(),
            NetworkTimeout = TimeSpan.FromSeconds(AiClientFactory.RequestTimeoutSeconds)
        };
        if (httpClient is not null)
        {
            openAIClientOptions.Transport = new HttpClientPipelineTransport(httpClient);
        }

        return openAIClientOptions;
    }

    // Keep shared wire-format handling beneath the framework's function loop.
    private sealed class CompatibleChatClient(IChatClient innerIChatClient, Action<ChatCompletionOptions>? configureOptionsAction) : DelegatingChatClient(innerIChatClient)
    {
        // Each model turn gets fresh SDK options without changing the lesson's ChatOptions.
        public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messagesChatMessages,
            ChatOptions? chatOptions = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ChatOptions? requestChatOptions = chatOptions;
            if (chatOptions is not null && (chatOptions.ResponseFormat is ChatResponseFormatJson || chatOptions.Tools?.Count > 0))
            {
                requestChatOptions = chatOptions.Clone();
                requestChatOptions.RawRepresentationFactory = iChatClient =>
                {
                    var rawChatCompletionOptions = chatOptions.RawRepresentationFactory?.Invoke(iChatClient) as ChatCompletionOptions
                        ?? new ChatCompletionOptions();
                    configureOptionsAction?.Invoke(rawChatCompletionOptions);
                    return rawChatCompletionOptions;
                };
            }

            ChatResponse chatResponse = await base.GetResponseAsync(messagesChatMessages, requestChatOptions, cancellationToken);
            // Both direct Gemini and Gemini behind OpenRouter can return opaque tool metadata.
            if (chatResponse.RawRepresentation is ChatCompletion chatCompletion && chatCompletion.ToolCalls.Count > 0)
            {
                foreach (ChatMessage chatMessage in chatResponse.Messages.Where(chatMessage => chatMessage.Role == ChatRole.Assistant))
                {
                    chatMessage.RawRepresentation = new AssistantChatMessage(chatCompletion);
                }
            }
            return chatResponse;
        }
    }
}
