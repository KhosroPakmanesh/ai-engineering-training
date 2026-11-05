using Xunit;

public class ProviderSettingsTests
{
    // Describing the settings must not print the API key.
    [Fact]
    public void Settings_description_does_not_expose_credentials()
    {
        // Arrange
        var providerSettings = new ProviderSettings(
            "Gemini",
            "secret-test-key",
            "gemini-model",
            new Uri("https://example.com"));

        // Act
        Assert.DoesNotContain(
            providerSettings.KeyString,
            providerSettings.ToString());

        // Assert
        Assert.Contains(
            providerSettings.ModelString,
            providerSettings.ToString());
    }
}
