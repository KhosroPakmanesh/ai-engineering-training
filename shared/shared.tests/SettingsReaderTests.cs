using Xunit;

public class SettingsReaderTests
{
    // Embedding configuration must not require, replace, or change the existing chat model.
    [Theory]
    [InlineData("openrouter", "OPENROUTER")]
    [InlineData("gemini", "GEMINI")]
    public void Embeddings_use_only_the_selected_embedding_model(string provider, string prefix)
    {
        ProviderSettings settings = SettingsReader.ParseSettings(
            [$"AI_PROVIDER={provider}", $"{prefix}_API_KEY=test-key", $"{prefix}_EMBEDDING_MODEL=embedding-model"],
            useEmbeddingModel: true);
        Assert.Equal("embedding-model", settings.Model);
        Assert.Equal("test-key", settings.Key);
    }

    // A chat model is never silently used as an embedding model.
    [Theory]
    [InlineData("openrouter", "OPENROUTER")]
    [InlineData("gemini", "GEMINI")]
    public void Missing_embedding_model_is_rejected_without_affecting_chat_settings(string provider, string prefix)
    {
        List<string> lines = [$"AI_PROVIDER={provider}", $"{prefix}_API_KEY=test-key", $"{prefix}_MODEL=chat-model"];
        Assert.Equal("chat-model", SettingsReader.ParseSettings(lines).Model);
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            SettingsReader.ParseSettings(lines, useEmbeddingModel: true));
        Assert.Contains($"{prefix}_EMBEDDING_MODEL", error.Message);
    }

    // Comments, quotes, and spaces around settings must not change their values.
    [Fact]
    public void Settings_accept_comments_quotes_and_equals_in_values()
    {
        ProviderSettings settings = SettingsReader.ParseSettings(
        [
            "# Local settings",
            "",
            " OPENROUTER_API_KEY = 'test-key=value' ",
            "OPENROUTER_MODEL=\"vendor/model\""
        ]);
        Assert.Equal("test-key=value", settings.Key);
        Assert.Equal("vendor/model", settings.Model);
        Assert.Equal("OpenRouter", settings.Name);
        Assert.Equal(new Uri("https://openrouter.ai/api/v1"), settings.Endpoint);
    }

    // Only the selected provider needs a real key and model; unused example values are allowed.
    [Theory]
    [InlineData("openrouter", "OPENROUTER", "GEMINI")]
    [InlineData("gemini", "GEMINI", "OPENROUTER")]
    public void Only_selected_provider_requires_credentials(string provider, string selected, string unused)
    {
        ProviderSettings settings = SettingsReader.ParseSettings(
        [
            $"AI_PROVIDER={provider}",
            $"{selected}_API_KEY=test-key",
            $"{selected}_MODEL=test-model",
            $"{unused}_API_KEY=your-key",
            $"{unused}_MODEL=your-model-id"
        ]);
        Assert.Equal("test-key", settings.Key);
        Assert.Equal("test-model", settings.Model);
    }

    // Reject missing or example keys and model names before making a paid request.
    [Theory]
    [InlineData("openrouter", "OPENROUTER")]
    [InlineData("gemini", "GEMINI")]
    public void Settings_reject_missing_values_and_placeholders(string provider, string prefix)
    {
        string[][] invalidSettings =
        [
            [],
            [$"{prefix}_API_KEY=test-key"],
            [$"{prefix}_API_KEY=your-key", $"{prefix}_MODEL=test-model"],
            [$"{prefix}_API_KEY=test-key", $"{prefix}_MODEL=your-model-id"],
            [$"{prefix}_API_KEY=test-key", $"{prefix}_MODEL=your-model"],
            [$"{prefix}_API_KEY= ", $"{prefix}_MODEL=test-model"],
            [$"{prefix}_API_KEY=test-key", $"{prefix}_MODEL= "]
        ];
        foreach (string[] lines in invalidSettings)
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            SettingsReader.ParseSettings([$"AI_PROVIDER={provider}", .. lines]));
            Assert.Contains($"{prefix}_API_KEY", error.Message);
            Assert.Contains($"{prefix}_MODEL", error.Message);
        }
    }

    // Reject an unknown provider name even if another provider has a valid key.
    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    public void Settings_reject_unsupported_provider_even_with_valid_credentials(string provider)
    {
        Assert.Throws<InvalidOperationException>(() => SettingsReader.ParseSettings(
        [
            $"AI_PROVIDER={provider}",
            "OPENROUTER_API_KEY=test-key",
            "OPENROUTER_MODEL=vendor/model"
        ]));
    }
}
