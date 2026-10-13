using System.ClientModel;

// Shared plumbing only. Each experiment keeps its actual SDK request in its own source file.
public static class OpenRouter
{
    // SDK network timeouts and cancellation use the same request deadline.
    public const int RequestTimeoutSeconds = 120;

    // Project references copy the one source .env beside executables and test assemblies.
    public static async Task<(string Key, string Model)> ReadSettingsAsync(
        string folder, CancellationToken cancellationToken = default)
    {
        string envFile = Path.Combine(folder, ".env");
        if (!File.Exists(envFile))
            throw new InvalidOperationException("Create shared/shared/.env from shared/shared/.env.example, fill in the key/model, and rebuild. No requests were made.");

        return ParseSettings(await File.ReadAllLinesAsync(envFile, cancellationToken));
    }

    // Keep parsing separate so configuration can be checked without credentials or an API call.
    public static (string Key, string Model) ParseSettings(IEnumerable<string> lines)
    {
        Dictionary<string, string> settings = ReadValues(lines);
        if (!settings.TryGetValue("OPENROUTER_API_KEY", out string? key) || string.IsNullOrWhiteSpace(key) || key == "your-key" ||
            !settings.TryGetValue("OPENROUTER_MODEL", out string? model) || string.IsNullOrWhiteSpace(model) || model == "your-model-id")
            throw new InvalidOperationException("Fill in OPENROUTER_API_KEY and OPENROUTER_MODEL in shared/shared/.env, then rebuild. No requests were made.");
        return (key, model);
    }

    // Split at the first equals sign so an equals sign inside a value is preserved.
    private static Dictionary<string, string> ReadValues(IEnumerable<string> lines)
    {
        var settings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in lines)
        {
            if (line.TrimStart().StartsWith('#')) continue;
            string[] pair = line.Split('=', 2);
            if (pair.Length == 2) settings[pair[0].Trim()] = pair[1].Trim().Trim('"', '\'');
        }
        return settings;
    }

    // Reject unusable responses without trimming or otherwise changing valid text.
    public static string ReadResponse(string? text, bool wasTruncated)
    {
        if (wasTruncated)
            throw new InvalidDataException("The provider truncated its response at the output token limit.");
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException("The provider returned no response text. Check the model and its reasoning/output token budget.");
        return text;
    }

    // Keep the provider's failure visible before suggesting the next useful check.
    public static void WriteError(ClientResultException error)
    {
        Console.Error.WriteLine($"OpenRouter request failed (HTTP {error.Status}): {error.Message}");
        WriteProviderDetails(error);
        Console.Error.WriteLine(ErrorAdvice(error.Status));
    }

    // Some SDK errors omit response details; others already include them in the message.
    private static void WriteProviderDetails(ClientResultException error)
    {
        string? details = error.GetRawResponse()?.Content?.ToString();
        if (!string.IsNullOrWhiteSpace(details) && !error.Message.Contains(details, StringComparison.Ordinal))
            Console.Error.WriteLine(details);
    }

    // Status-specific guidance distinguishes configuration failures from provider availability.
    private static string ErrorAdvice(int status) => status switch
    {
        400 => "Check that the model/provider supports the requested Chat Completions parameters.",
        401 => "Check OPENROUTER_API_KEY in shared/shared/.env, then rebuild.",
        402 => "The account has insufficient credits for this request.",
        404 => "Check OPENROUTER_MODEL in shared/shared/.env, then rebuild.",
        429 => "The provider is rate limiting requests or is at capacity. Wait and run again.",
        >= 500 => "OpenRouter or the upstream provider is unavailable. Try again later.",
        _ => "Check the provider error above."
    };
}
