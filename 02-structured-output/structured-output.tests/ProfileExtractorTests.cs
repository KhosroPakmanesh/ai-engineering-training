using Xunit;

public class ProfileExtractorTests
{
    // A valid example must pass JSON checks, record conversion, evidence checks, and skill cleanup.
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
