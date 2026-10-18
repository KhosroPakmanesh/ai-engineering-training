using Xunit;

// These tests share ProfileValidator static state with the other validation callers.
[Collection("Profile validation")]
public class ProfileExtractorTests
{
    // A known valid profile exercises the complete local acceptance flow.
    [Fact]
    public void Example_can_be_parsed_into_records()
    {
        string jsonResume = ProfileTestData.ReadSample("profile.example.json");
        JsonResumeSchemaValidator.ValidateSchema(jsonResume);
        CandidateProfile profile = ProfileExtractor.ExtractProfile(jsonResume);
        ProfileValidator.ValidateProfile(profile, ProfileTestData.ReadSample("resume.md"), "resume.md");
        profile = ProfileNormalizer.NormalizeProfile(profile);

        Assert.Equal("Mira Example", profile.Name);
        Assert.Equal(2, profile.Experience.Count);
    }
}
