public static class SettingsReader
{
    // Read the copied settings so console runs and tests use the same configuration.
    public static async Task<ProviderSettings> ReadSettingsAsync(
        string folder, CancellationToken cancellationToken = default, bool useEmbeddingModel = false)
    {
        string envFile = Path.Combine(folder, ".env");
        if (!File.Exists(envFile))
            throw new InvalidOperationException("Create shared/shared/.env from shared/shared/.env.example, fill in the key/model, and rebuild. No requests were made.");

        return ParseSettings(await File.ReadAllLinesAsync(envFile, cancellationToken), useEmbeddingModel);
    }
    // Require a key and model only for the selected provider.
    public static ProviderSettings ParseSettings(IEnumerable<string> lines, bool useEmbeddingModel = false)
    {
        Dictionary<string, string> settings = ReadValues(lines);
        var factory = new AiClientFactory();
        IAiProviderStrategy provider = factory.SelectProvider(settings.GetValueOrDefault("AI_PROVIDER", "openrouter"));
        string prefix = provider.SettingsPrefix;
        string modelKey = useEmbeddingModel ? $"{prefix}_EMBEDDING_MODEL" : $"{prefix}_MODEL";
        if (!settings.TryGetValue($"{prefix}_API_KEY", out string? key) || string.IsNullOrWhiteSpace(key) || key == "your-key" ||
            !settings.TryGetValue(modelKey, out string? model) || string.IsNullOrWhiteSpace(model) || model is "your-model" or "your-model-id")
            throw new InvalidOperationException($"Fill in {prefix}_API_KEY and {modelKey} in shared/shared/.env, then rebuild. No requests were made.");
        return new(provider.Name, key, model, provider.Endpoint);
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
