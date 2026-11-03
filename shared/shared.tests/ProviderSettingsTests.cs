using Xunit;

public class ProviderSettingsTests
{
    // Describing the settings must not print the API key.
    [Fact]
    public void Settings_description_does_not_expose_credentials()
    {
        var providerSettings = new ProviderSettings("Gemini", "secret-test-key", "gemini-model", new Uri("https://example.com"));
        Assert.DoesNotContain(providerSettings.KeyString, providerSettings.ToString());
        Assert.Contains(providerSettings.ModelString, providerSettings.ToString());
    }
}
