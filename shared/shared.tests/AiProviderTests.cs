using Xunit;

public class AiProviderTests
{
    // Selected settings must configure the SDK endpoint and model without a real request.
    [Theory]
    [InlineData("openrouter", "OpenRouter", "https://openrouter.ai/api/v1")]
    [InlineData("gemini", "Gemini", "https://generativelanguage.googleapis.com/v1beta/openai/")]
    [InlineData("GEMINI", "Gemini", "https://generativelanguage.googleapis.com/v1beta/openai/")]
    public void Selected_provider_controls_settings_and_client(string provider, string name, string endpoint)
    {
        ProviderSettings settings = SettingsReader.ParseSettings(
        [
            $"AI_PROVIDER={provider}",
            "OPENROUTER_API_KEY=openrouter-key",
            "OPENROUTER_MODEL=vendor/model",
            "GEMINI_API_KEY=gemini-key",
            "GEMINI_MODEL=gemini-model"
        ]);

        Assert.Equal(name, settings.Name);
        Assert.Equal(new Uri(endpoint), settings.Endpoint);
        Assert.Equal(name == "Gemini" ? "gemini-key" : "openrouter-key", settings.Key);
        Assert.Equal(name == "Gemini" ? "gemini-model" : "vendor/model", settings.Model);
        var client = AiProvider.CreateChatClient(settings);

        #pragma warning disable OPENAI001
        Assert.Equal(new Uri(endpoint), client.Endpoint);
        Assert.Equal(settings.Model, client.Model);
        #pragma warning restore OPENAI001
    }

    // A lesson must not accept an empty or unfinished answer as a valid result.
    [Fact]
    public void Response_checks_reject_empty_or_truncated_text_and_preserve_valid_text()
    {
        const string original = "  A complete response.\n";
        Assert.Equal(original, AiProvider.ReadResponse(original, wasTruncated: false));
        Assert.Throws<InvalidDataException>(() => AiProvider.ReadResponse(null, wasTruncated: false));
        Assert.Throws<InvalidDataException>(() => AiProvider.ReadResponse("  ", wasTruncated: false));
        Assert.Throws<InvalidDataException>(() => AiProvider.ReadResponse("Incomplete but nonempty.", wasTruncated: true));
    }
}
