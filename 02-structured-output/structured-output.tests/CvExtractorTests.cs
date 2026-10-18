using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

// These tests share ProfileValidator static state with the other validation callers.
[Collection("Profile validation")]
public class CvExtractorTests(ITestOutputHelper output)
{
    // Model responses pass through the same local checks and cleanup as Program.cs.
    [Theory]
    [InlineData("resume.md", false)]
    [InlineData("resume-ambiguous.md", true)]
    [Trait("Category", "Integration")]
    public async Task Extract_returns_a_typed_profile_and_preserves_unknowns(string resumeFileName, bool hasMissingFacts)
    {
        string rawStringResume = ProfileTestData.ReadSample(resumeFileName);
        string jsonResume = await CvExtractor.ExtractAsync(rawStringResume, resumeFileName);
        JsonResumeSchemaValidator.ValidateSchema(jsonResume);
        CandidateProfile profile = ProfileExtractor.ExtractProfile(jsonResume);
        ProfileValidator.ValidateProfile(profile, rawStringResume, resumeFileName);
        profile = ProfileNormalizer.NormalizeProfile(profile);

        JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
        output.WriteLine(JsonSerializer.Serialize(profile, jsonOptions));

        AssertPopulatedProfile(profile);
        if (hasMissingFacts)
            AssertMissingFacts(profile);
        else
        {
            Assert.Equal(2, profile.Experience.Count);
            Assert.Contains(profile.Education, item => item.Degree is not null);
        }
    }

    // Both model responses must contain useful typed collections.
    private static void AssertPopulatedProfile(CandidateProfile profile)
    {
        Assert.False(string.IsNullOrWhiteSpace(profile.Name));
        Assert.NotEmpty(profile.Experience);
        Assert.NotEmpty(profile.Education);
        Assert.NotEmpty(profile.Skills);
    }

    // Missing and ambiguous source facts must remain unknown.
    private static void AssertMissingFacts(CandidateProfile profile)
    {
        Assert.Null(profile.Phone);
        Assert.All(profile.Education, item => Assert.Null(item.Degree));
        Assert.All(profile.Experience, item =>
        {
            Assert.Null(item.StartDate);
            Assert.Null(item.EndDate);
            Assert.Null(item.DurationMonths);
        });
        Assert.All(profile.Skills, item => Assert.Null(item.Proficiency));
        Assert.Equal(2, profile.Skills.Count);
    }
}
