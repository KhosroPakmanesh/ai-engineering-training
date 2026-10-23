using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;
using Xunit.Abstractions;

public class CvExtractorTests(ITestOutputHelper output)
{
    // A local fixture checks the post-request stages without credentials or a model call.
    [Fact]
    public void Example_response_passes_schema_conversion_evidence_and_normalization()
    {
        string rawStringResume = ProfileTestData.ReadSample("resume.md");
        string jsonResume = ProfileTestData.ReadSample("profile.example.json");
        var rawResponse = new AgentResponse(new ChatMessage(ChatRole.Assistant, jsonResume));
        var response = new AgentResponse<CandidateProfile>(rawResponse, JsonSerializerOptions.Web);

        Assert.Equal(jsonResume, response.Text);
        JsonResumeSchemaValidator.ValidateSchema(response.Text);
        CandidateProfile profile = response.Result;
        ProfileValidator.ValidateProfile(profile, rawStringResume, "resume.md");
        profile = ProfileNormalizer.NormalizeProfile(profile);
        Assert.Equal("Mira Example", profile.Name);
        Assert.Equal(2, profile.Experience.Count);
    }

    // Model responses pass through the same local checks and cleanup as Program.cs.
    [Theory]
    [InlineData("resume.md", false)]
    [InlineData("resume-ambiguous.md", true)]
    [Trait("Category", "Integration")]
    public async Task Extract_returns_a_typed_profile_and_preserves_unknowns(string resumeFileName, bool hasMissingFacts)
    {
        string rawStringResume = ProfileTestData.ReadSample(resumeFileName);
        AgentResponse<CandidateProfile> response = await CvExtractor.ExtractAsync(rawStringResume, resumeFileName);
        JsonResumeSchemaValidator.ValidateSchema(response.Text);
        CandidateProfile profile = response.Result;
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

    // Both resumes must produce a name and nonempty experience, education, and skill lists.
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
