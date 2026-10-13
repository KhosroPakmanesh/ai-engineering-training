using Xunit;

// Shared plumbing checks run once in shared.tests without API requests.
public class OpenRouterTests
{
    // Harmless .env formatting should preserve the exact key and model values.
    [Fact]
    public void Settings_accept_comments_quotes_and_equals_in_values()
    {
        (string key, string model) = OpenRouter.ParseSettings(
        [
            "# Local settings",
            "",
            " OPENROUTER_API_KEY = 'test-key=value' ",
            "OPENROUTER_MODEL=\"vendor/model\""
        ]);
        Assert.Equal("test-key=value", key);
        Assert.Equal("vendor/model", model);
    }

    // Invalid source settings must fail locally instead of reaching the remote provider.
    [Fact]
    public void Settings_reject_missing_values_and_placeholders()
    {
        string[][] invalidSettings =
        [
            [],
            ["OPENROUTER_API_KEY=test-key"],
            ["OPENROUTER_API_KEY=your-key", "OPENROUTER_MODEL=vendor/model"],
            ["OPENROUTER_API_KEY=test-key", "OPENROUTER_MODEL=your-model-id"],
            ["OPENROUTER_API_KEY= ", "OPENROUTER_MODEL=vendor/model"],
            ["OPENROUTER_API_KEY=test-key", "OPENROUTER_MODEL= "]
        ];
        foreach (string[] lines in invalidSettings)
            Assert.Throws<InvalidOperationException>(() => OpenRouter.ParseSettings(lines));
    }

    // Guarding a response must preserve valid whitespace while rejecting empty or incomplete text.
    [Fact]
    public void Response_checks_reject_empty_or_truncated_text_and_preserve_valid_text()
    {
        const string original = "  A complete response.\n";
        Assert.Equal(original, OpenRouter.ReadResponse(original, wasTruncated: false));
        Assert.Throws<InvalidDataException>(() => OpenRouter.ReadResponse(null, wasTruncated: false));
        Assert.Throws<InvalidDataException>(() => OpenRouter.ReadResponse("  ", wasTruncated: false));
        Assert.Throws<InvalidDataException>(() => OpenRouter.ReadResponse("Incomplete but nonempty.", wasTruncated: true));
    }
}
