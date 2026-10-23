using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

public class JsonResumeSchemaValidatorTests
{
    // Check both broken JSON syntax and valid JSON that does not match the required profile format.
    [Fact]
    public void Invalid_json_is_rejected_before_it_becomes_a_profile()
    {
        Assert.ThrowsAny<JsonException>(() => JsonResumeSchemaValidator.ValidateSchema("{\"name\":"));
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            JsonResumeSchemaValidator.ValidateSchema("{\"skills\":42}"));
        Assert.Contains("/skills", error.Message);
    }

    // AgentResponse.Result can read a first object; the schema stage must reject extra objects and duplicate field names.
    [Theory]
    [InlineData("{} {}")]
    [InlineData("{\"name\":\"first\",\"name\":\"second\"}")]
    public void Ambiguous_json_is_rejected_before_framework_conversion(string jsonResume)
    {
        Assert.ThrowsAny<JsonException>(() => JsonResumeSchemaValidator.ValidateSchema(jsonResume));
    }

    // An otherwise valid profile with an unknown field must fail before record conversion can discard that field.
    [Fact]
    public void Unknown_fields_in_the_original_json_are_rejected()
    {
        JsonObject profileJson = JsonNode.Parse(ProfileTestData.ReadSample("profile.example.json"))!.AsObject();
        profileJson["unexpected"] = true;

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            JsonResumeSchemaValidator.ValidateSchema(profileJson.ToJsonString()));
        Assert.Contains("unexpected", error.Message);
    }
}
