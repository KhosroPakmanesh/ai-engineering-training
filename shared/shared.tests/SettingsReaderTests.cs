using Xunit;

public class SettingsReaderTests
{
    // Ordinary settings-file formatting must preserve the selected values.
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

    // Unused provider placeholders must not prevent the selected provider from working.
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

    // Incomplete selected credentials must fail locally before a paid request.
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

    // A valid key must not silently choose a provider for an unsupported selector.
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
