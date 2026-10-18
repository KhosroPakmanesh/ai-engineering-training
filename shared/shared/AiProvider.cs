using System.ClientModel;
using System.ClientModel.Primitives;
using OpenAI;
using OpenAI.Chat;

public static class AiProvider
{
    public const int RequestTimeoutSeconds = 120;

    public static ChatClient CreateChatClient(ProviderSettings settings) => new(
        settings.Model, new ApiKeyCredential(settings.Key), new OpenAIClientOptions
        {
            Endpoint = settings.Endpoint,
            RetryPolicy = new AiRetryPolicy(),
            NetworkTimeout = TimeSpan.FromSeconds(RequestTimeoutSeconds)
        });
    public static string ReadResponse(string? text, bool wasTruncated)
    {
        if (wasTruncated)
            throw new InvalidDataException("The provider truncated its response at the output token limit.");
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException("The provider returned no response text. Check the model and its reasoning/output token budget.");
        return text;
    }
    public static void WriteError(ClientResultException error)
    {
        Console.Error.WriteLine($"AI provider request failed (HTTP {error.Status}): {error.Message}");
        WriteProviderDetails(error);
        Console.Error.WriteLine(ErrorAdvice(error.Status));
    }
    private static void WriteProviderDetails(ClientResultException error)
    {
        string? details = error.GetRawResponse()?.Content?.ToString();
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
