using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

public class StructuredOutputTests(ITestOutputHelper output)
{
    // Two real requests, through the same method Program.cs calls, using the same .env.
    [Theory]
    [InlineData("resume.md", false)]
    [InlineData("resume-ambiguous.md", true)]
    [Trait("Category", "Integration")]
    public async Task Extract_returns_a_typed_profile_and_preserves_unknowns(string resumeFile, bool hasMissingFacts)
    {
        string resume = ReadSample(resumeFile);
        string json = await CvExtractor.ExtractAsync(resume, resumeFile);
        CandidateProfile profile = ProfileValidator.ParseAndValidate(json, resume, resumeFile);
        output.WriteLine(JsonSerializer.Serialize(profile, ProfileValidator.JsonOptions));

        Assert.False(string.IsNullOrWhiteSpace(profile.Name));
        Assert.NotEmpty(profile.Experience);
        Assert.NotEmpty(profile.Education);
        Assert.NotEmpty(profile.Skills);
        if (hasMissingFacts)
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
            Assert.Equal(2, profile.Skills.Length);
        }
        else
        {
            Assert.Equal(2, profile.Experience.Length);
            Assert.Contains(profile.Education, item => item.Degree is not null);
        }
    }

    [Fact]
    public void Example_can_be_parsed_into_records()
    {
        CandidateProfile profile = ProfileValidator.ParseAndValidate(
            ReadSample("profile.example.json"), ReadSample("resume.md"), "resume.md");
        Assert.Equal("Mira Example", profile.Name);
        Assert.Equal(2, profile.Experience.Length);
    }

    [Fact]
    public void Invalid_json_is_rejected_before_it_becomes_a_profile()
    {
        string resume = ReadSample("resume.md");
        Assert.ThrowsAny<JsonException>(() => ProfileValidator.ParseAndValidate("{\"name\":", resume, "resume.md"));
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ProfileValidator.ParseAndValidate("{\"skills\":42}", resume, "resume.md"));
        Assert.Contains("/skills", error.Message);
    }

    private static string ReadSample(string filename) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sample-data", filename));
}
