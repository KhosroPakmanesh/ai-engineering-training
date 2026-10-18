using System.Text.Json;
using Xunit;

public class JsonResumeSchemaValidatorTests
{
    // Malformed JSON and valid JSON with an invalid schema fail at different checks.
    [Fact]
    public void Invalid_json_is_rejected_before_it_becomes_a_profile()
    {
        Assert.ThrowsAny<JsonException>(() => JsonResumeSchemaValidator.ValidateSchema("{\"name\":"));
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            JsonResumeSchemaValidator.ValidateSchema("{\"skills\":42}"));
        Assert.Contains("/skills", error.Message);
    }
}
