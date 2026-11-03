public static class SettingsReader
{
    // Read the copied settings so console runs and tests use the same configuration.
    public static async Task<ProviderSettings> ReadSettingsAsync(string folderString, CancellationToken cancellationToken = default,
        bool useEmbeddingModelBool = false)
    {
        string envFileString = Path.Combine(folderString, ".env");
        if (!File.Exists(envFileString))
        {
            throw new InvalidOperationException("Create shared/shared/.env from shared/shared/.env.example, fill in the key/model, and rebuild. No requests were made.");
        }

        return ParseSettings(await File.ReadAllLinesAsync(envFileString, cancellationToken), useEmbeddingModelBool);
    }

    // Require a key and model only for the selected provider.
    public static ProviderSettings ParseSettings(IEnumerable<string> lineStrings, bool useEmbeddingModelBool = false)
    {
        Dictionary<string, string> settingStringsDictionary = ReadValues(lineStrings);
        var aiClientFactory = new AiClientFactory();
        IAiProviderStrategy selectedIAiProviderStrategy = aiClientFactory.SelectProvider(settingStringsDictionary.GetValueOrDefault("AI_PROVIDER", "openrouter"));
        string prefixString = selectedIAiProviderStrategy.SettingsPrefixString;
        string modelKeyString = useEmbeddingModelBool ? $"{prefixString}_EMBEDDING_MODEL" : $"{prefixString}_MODEL";
        if (!settingStringsDictionary.TryGetValue($"{prefixString}_API_KEY", out string? keyString) || string.IsNullOrWhiteSpace(keyString) || keyString == "your-key" ||
            !settingStringsDictionary.TryGetValue(modelKeyString, out string? modelString) || string.IsNullOrWhiteSpace(modelString) || modelString is "your-model" or "your-model-id")
        {
            throw new InvalidOperationException($"Fill in {prefixString}_API_KEY and {modelKeyString} in shared/shared/.env, then rebuild. No requests were made.");
        }

        return new(selectedIAiProviderStrategy.NameString, keyString, modelString, selectedIAiProviderStrategy.EndpointUri);
    }

    // Split only at the first equals sign because a setting value may contain another.
    private static Dictionary<string, string> ReadValues(IEnumerable<string> lineStrings)
    {
        var settingStringsDictionary = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string lineString in lineStrings)
        {
            if (lineString.TrimStart().StartsWith('#'))
            {
                continue;
            }

            string[] pairStrings = lineString.Split('=', 2);
            if (pairStrings.Length == 2)
            {
                settingStringsDictionary[pairStrings[0].Trim()] = pairStrings[1].Trim().Trim('"', '\'');
            }
        }

        return settingStringsDictionary;
    }
}
