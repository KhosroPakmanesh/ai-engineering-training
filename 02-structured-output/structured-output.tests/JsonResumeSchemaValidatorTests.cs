using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

public class JsonResumeSchemaValidatorTests
{
    // Check both broken JSON syntax and valid JSON that does not match the required profile format.
    [Fact]
    public void Invalid_json_is_rejected_before_it_becomes_a_profile()
    {
        // Act
        Assert.ThrowsAny<JsonException>(() => JsonResumeSchemaValidator.ValidateSchema("{\"name\":"));

        // Assert
        InvalidDataException errorInvalidDataException = Assert.Throws<InvalidDataException>(() =>
            JsonResumeSchemaValidator.ValidateSchema("{\"skills\":42}"));
        Assert.Contains(
            "/skills",
            errorInvalidDataException.Message);
    }

    // AgentResponse.Result can read a first object; the schema stage must reject extra objects and duplicate field
    // names.
    [Theory]
    [InlineData("{} {}")]
    [InlineData("{\"name\":\"first\",\"name\":\"second\"}")]
    public void Ambiguous_json_is_rejected_before_framework_conversion(string jsonResumeString)
    {
        // Act
        // Assert
        Assert.ThrowsAny<JsonException>(() => JsonResumeSchemaValidator.ValidateSchema(jsonResumeString));
    }

    // An otherwise valid profile with an unknown field must fail before record conversion can discard that field.
    [Fact]
    public void Unknown_fields_in_the_original_json_are_rejected()
    {
        // Arrange
        JsonObject profileJsonObject = JsonNode.Parse(ProfileTestData.ReadSample("profile.example.json"))!.AsObject();
        profileJsonObject["unexpected"] = true;

        // Act
        InvalidDataException errorInvalidDataException = Assert.Throws<InvalidDataException>(() =>
            JsonResumeSchemaValidator.ValidateSchema(profileJsonObject.ToJsonString()));

        // Assert
        Assert.Contains(
            "unexpected",
            errorInvalidDataException.Message);
    }
}
