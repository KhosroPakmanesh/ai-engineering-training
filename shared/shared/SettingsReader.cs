public static class SettingsReader
{
    // Read the copied settings so console runs and tests use the same configuration.
    public static async Task<ProviderSettings> ReadSettingsAsync(
        string folder, CancellationToken cancellationToken = default)
    {
        string envFile = Path.Combine(folder, ".env");
        if (!File.Exists(envFile))
            throw new InvalidOperationException("Create shared/shared/.env from shared/shared/.env.example, fill in the key/model, and rebuild. No requests were made.");

        return ParseSettings(await File.ReadAllLinesAsync(envFile, cancellationToken));
    }
    // Require a key and model only for the selected provider.
    public static ProviderSettings ParseSettings(IEnumerable<string> lines)
    {
        Dictionary<string, string> settings = ReadValues(lines);
        string provider = settings.GetValueOrDefault("AI_PROVIDER", "openrouter").ToLowerInvariant();
        (string name, string prefix, string endpoint) = provider switch
        {
            "openrouter" => ("OpenRouter", "OPENROUTER", "https://openrouter.ai/api/v1"),
            "gemini" => ("Gemini", "GEMINI", "https://generativelanguage.googleapis.com/v1beta/openai/"),
            _ => throw new InvalidOperationException("Set AI_PROVIDER to openrouter or gemini in shared/shared/.env, then rebuild. No requests were made.")
        };
        if (!settings.TryGetValue($"{prefix}_API_KEY", out string? key) || string.IsNullOrWhiteSpace(key) || key == "your-key" ||
            !settings.TryGetValue($"{prefix}_MODEL", out string? model) || string.IsNullOrWhiteSpace(model) || model is "your-model" or "your-model-id")
            throw new InvalidOperationException($"Fill in {prefix}_API_KEY and {prefix}_MODEL in shared/shared/.env, then rebuild. No requests were made.");
        return new(name, key, model, new Uri(endpoint));
    }
    // Split only at the first equals sign because a setting value may contain another.
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
}
