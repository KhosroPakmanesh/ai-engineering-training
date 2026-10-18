using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

public static class AiProvider
{
    public const int RequestTimeoutSeconds = 120;
    public const int MaxRetries = AiRetryPolicy.MaxRetries;

    // Keep model calls in one place so lessons do not need provider-specific setup.
    public static Task<AiResponse> RequestAsync(
        ProviderSettings settings, AiRequest request, CancellationToken cancellationToken = default) =>
        RequestAsync(settings, request, httpClient: null, cancellationToken);
    // Tests supply an HTTP client that returns local responses; applications call the real provider.
    public static async Task<AiResponse> RequestAsync(
        ProviderSettings settings, AiRequest request, HttpClient? httpClient,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ChatOptions chatOptions = CreateChatOptions(settings, request);
        using IChatClient chatClient = CreateChatClient
            (settings, httpClient).AsIChatClient();
        ChatClientAgent agent = new(chatClient, new ChatClientAgentOptions
        {
            Name = request.Name,
            ChatOptions = chatOptions
        });

        using var timeout = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(RequestTimeoutSeconds));
        var timer = Stopwatch.StartNew();

        try
        {
            // Start without earlier messages so one answer cannot influence the next call.
            AgentResponse response = await agent
                .RunAsync(request.Input, cancellationToken: timeout.Token);
            timer.Stop();

            return new(
                Text: ReadResponse(response.Text,
                    response.RawRepresentation is ChatResponse chatResponse
                    && chatResponse.FinishReason == ChatFinishReason.Length),
                Usage: ReadUsage(response.Usage),
                LatencyMilliseconds: timer.ElapsedMilliseconds);
        }
        catch (ClientResultException error)
        {
            throw new AiRequestException(error.Status, error.Message,
                error.GetRawResponse()?.Content?.ToString(), error);
        }
    }
    // Reject missing or cut-short answers before a lesson uses them.
    public static string ReadResponse(string? text, bool wasTruncated)
    {
        if (wasTruncated)
            throw new InvalidDataException
                ("The provider truncated its response at the output token limit.");
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException
                ("The provider returned no response text. Check the model and its reasoning/output token budget.");
        return text;
    }
    // Show the API failure and suggest what the learner should check next.
    public static void WriteError(AiRequestException error)
    {
        Console.Error.WriteLine($"AI provider request failed (HTTP {error.Status}): {error.Message}");
        WriteProviderDetails(error);
        Console.Error.WriteLine(ErrorAdvice(error.Status));
    }

    // Keep the lesson's settings and JSON format unchanged when switching providers.
    private static ChatOptions CreateChatOptions(ProviderSettings settings, AiRequest request)
    {
        ChatOptions chatOptions = new()
        {
            Instructions = request.Instructions,
            Temperature = request.Temperature,
            MaxOutputTokens = request.MaxOutputTokens
        };

        if (request.ResponseSchema is { } responseSchema)
        {
            var providerOptions = new OpenAI.Chat.ChatCompletionOptions();
            if (settings.Name == "OpenRouter")
            {
                // Ask OpenRouter to use a provider that supports the requested settings.
#pragma warning disable SCME0001
                providerOptions.Patch.Set("$.provider"u8, """{"require_parameters":true}"""u8);
#pragma warning restore SCME0001
            }

            chatOptions.AdditionalProperties = new() { ["strict"] = true };
            chatOptions.RawRepresentationFactory = _ => providerOptions;
            chatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema
                (JsonElement.Parse(responseSchema.Schema), responseSchema.Name);
        }

        return chatOptions;
    }
    // Set the model, API address, retries, and time limit here so lessons do not repeat the setup.
    private static OpenAI.Chat.ChatClient CreateChatClient(ProviderSettings settings, HttpClient? httpClient)
    {
        OpenAIClientOptions clientOptions = new()
        {
            Endpoint = settings.Endpoint,
            RetryPolicy = new AiRetryPolicy(),
            NetworkTimeout = TimeSpan.FromSeconds(RequestTimeoutSeconds)
        };
        if (httpClient is not null) clientOptions.Transport =
                new HttpClientPipelineTransport(httpClient);

        return new(settings.Model, new ApiKeyCredential(settings.Key), clientOptions);
    }
    // Leave missing token counts as null because missing does not mean zero.
    private static AiTokenUsage? ReadUsage(UsageDetails? usage) => usage is null
        || (usage.InputTokenCount is null
        && usage.OutputTokenCount is null && usage.TotalTokenCount is null)
        ? null : new(usage.InputTokenCount, usage.OutputTokenCount, usage.TotalTokenCount);
    private static void WriteProviderDetails(AiRequestException error)
    {
        string? details = error.ResponseDetails;
        if (!string.IsNullOrWhiteSpace(details) && !error.Message.Contains(details, StringComparison.Ordinal))
            Console.Error.WriteLine(details);
    }
    private static string ErrorAdvice(int status) => status switch
    {
        400 => "Check that the model/provider supports the requested Chat Completions parameters.",
        401 or 403 => "Check the API key and access permissions for AI_PROVIDER (OPENROUTER_API_KEY or GEMINI_API_KEY) in shared/shared/.env, then rebuild.",
        402 => "The account has insufficient credits for this request.",
        404 => "Check the model for AI_PROVIDER (OPENROUTER_MODEL or GEMINI_MODEL) in shared/shared/.env and confirm support for the requested parameters, then rebuild.",
        429 => "The provider is rate limiting requests, has no remaining quota, or is at capacity. Check your quota or wait and run again.",
        >= 500 => "The selected provider or its upstream service is unavailable. Try again later.",
        _ => "Check the provider error above."
    };
}
