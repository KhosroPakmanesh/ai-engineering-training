using Xunit;

public class SettingsReaderTests
{
    // Embedding configuration must not require, replace, or change the existing chat model.
    [Theory]
    [InlineData(
        "openrouter",
        "OPENROUTER")]
    [InlineData(
        "gemini",
        "GEMINI")]
    public void Embeddings_use_only_the_selected_embedding_model(
        string providerString,
        string prefixString)
    {
        // Act
        ProviderSettings providerSettings = SettingsReader.ParseSettings(
            [
                $"AI_PROVIDER={providerString}",
                $"{prefixString}_API_KEY=test-key",
                $"{prefixString}_EMBEDDING_MODEL=embedding-model"
            ],
            useEmbeddingModelBool: true);

        // Assert
        Assert.Equal(
            "embedding-model",
            providerSettings.ModelString);
        Assert.Equal(
            "test-key",
            providerSettings.KeyString);
    }

    // A chat model is never silently used as an embedding model.
    [Theory]
    [InlineData(
        "openrouter",
        "OPENROUTER")]
    [InlineData(
        "gemini",
        "GEMINI")]
    public void Missing_embedding_model_is_rejected_without_affecting_chat_settings(
        string providerString,
        string prefixString)
    {
        // Arrange
        List<string> lineStrings =
        [
            $"AI_PROVIDER={providerString}",
            $"{prefixString}_API_KEY=test-key",
            $"{prefixString}_MODEL=chat-model"
        ];

        // Act
        Assert.Equal(
            "chat-model",
            SettingsReader.ParseSettings(lineStrings).ModelString);

        // Assert
        InvalidOperationException errorInvalidOperationException = Assert.Throws<InvalidOperationException>(() =>
            SettingsReader.ParseSettings(
                lineStrings,
                useEmbeddingModelBool: true));
        Assert.Contains(
            $"{prefixString}_EMBEDDING_MODEL",
            errorInvalidOperationException.Message);
    }

    // Comments, quotes, and spaces around settings must not change their values.
    [Fact]
    public void Settings_accept_comments_quotes_and_equals_in_values()
    {
        // Act
        ProviderSettings providerSettings = SettingsReader.ParseSettings(
        [
            "# Local settings",
            "",
            " OPENROUTER_API_KEY = 'test-key=value' ",
            "OPENROUTER_MODEL=\"vendor/model\""
        ]);

        // Assert
        Assert.Equal(
            "test-key=value",
            providerSettings.KeyString);
        Assert.Equal(
            "vendor/model",
            providerSettings.ModelString);
        Assert.Equal(
            "OpenRouter",
            providerSettings.NameString);
        Assert.Equal(
            new Uri("https://openrouter.ai/api/v1"),
            providerSettings.EndpointUri);
    }

    // Only the selected provider needs a real key and model; unused example values are allowed.
    [Theory]
    [InlineData(
        "openrouter",
        "OPENROUTER",
        "GEMINI")]
    [InlineData(
        "gemini",
        "GEMINI",
        "OPENROUTER")]
    public void Only_selected_provider_requires_credentials(
        string providerString,
        string selectedString,
        string unusedString)
    {
        // Act
        ProviderSettings providerSettings = SettingsReader.ParseSettings(
        [
            $"AI_PROVIDER={providerString}",
            $"{selectedString}_API_KEY=test-key",
            $"{selectedString}_MODEL=test-model",
            $"{unusedString}_API_KEY=your-key",
            $"{unusedString}_MODEL=your-model-id"
        ]);

        // Assert
        Assert.Equal(
            "test-key",
            providerSettings.KeyString);
        Assert.Equal(
            "test-model",
            providerSettings.ModelString);
    }

    // Reject missing or example keys and model names before making a paid request.
    [Theory]
    [InlineData(
        "openrouter",
        "OPENROUTER")]
    [InlineData(
        "gemini",
        "GEMINI")]
    public void Settings_reject_missing_values_and_placeholders(
        string providerString,
        string prefixString)
    {
        // Arrange
        string[][] invalidSettingsStringArrays =
        [
            [],
            [$"{prefixString}_API_KEY=test-key"],
            [$"{prefixString}_API_KEY=your-key", $"{prefixString}_MODEL=test-model"],
            [$"{prefixString}_API_KEY=test-key", $"{prefixString}_MODEL=your-model-id"],
            [$"{prefixString}_API_KEY=test-key", $"{prefixString}_MODEL=your-model"],
            [$"{prefixString}_API_KEY= ", $"{prefixString}_MODEL=test-model"],
            [$"{prefixString}_API_KEY=test-key", $"{prefixString}_MODEL= "]
        ];

        // Act
        // Assert
        foreach (string[] lineStrings in invalidSettingsStringArrays)
        {
            InvalidOperationException errorInvalidOperationException = Assert.Throws<InvalidOperationException>(() =>
            SettingsReader.ParseSettings([$"AI_PROVIDER={providerString}", .. lineStrings]));
            Assert.Contains(
                $"{prefixString}_API_KEY",
                errorInvalidOperationException.Message);
            Assert.Contains(
                $"{prefixString}_MODEL",
                errorInvalidOperationException.Message);
        }
    }

    // Reject an unknown provider name even if another provider has a valid key.
    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    public void Settings_reject_unsupported_provider_even_with_valid_credentials(string providerString)
    {
        // Act
        // Assert
        Assert.Throws<InvalidOperationException>(() => SettingsReader.ParseSettings(
        [
            $"AI_PROVIDER={providerString}",
            "OPENROUTER_API_KEY=test-key",
            "OPENROUTER_MODEL=vendor/model"
        ]));
    }
}
