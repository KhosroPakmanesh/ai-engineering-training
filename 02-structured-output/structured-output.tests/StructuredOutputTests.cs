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

        // Structure and explicit unknowns can be checked without judging the answer's prose.
        AssertPopulatedProfile(profile);
        if (hasMissingFacts)
            AssertMissingFacts(profile);
        else
        {
            Assert.Equal(2, profile.Experience.Length);
            Assert.Contains(profile.Education, item => item.Degree is not null);
        }
    }

    // A known fixture exercises the same schema and data checks as an agent response.
    [Fact]
    public void Example_can_be_parsed_into_records()
    {
        CandidateProfile profile = ProfileValidator.ParseAndValidate(
            ReadSample("profile.example.json"), ReadSample("resume.md"), "resume.md");
        Assert.Equal("Mira Example", profile.Name);
        Assert.Equal(2, profile.Experience.Length);
    }

    // JSON syntax and schema failures happen before the result becomes an accepted profile.
    [Fact]
    public void Invalid_json_is_rejected_before_it_becomes_a_profile()
    {
        string resume = ReadSample("resume.md");
        Assert.ThrowsAny<JsonException>(() => ProfileValidator.ParseAndValidate("{\"name\":", resume, "resume.md"));
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ProfileValidator.ParseAndValidate("{\"skills\":42}", resume, "resume.md"));
        Assert.Contains("/skills", error.Message);
    }

    // A correctly shaped object can still name a different source or invent a text fact.
    [Fact]
    public void Source_and_text_facts_must_match_their_evidence()
    {
        CandidateProfile profile = ReadExample() with
        {
            Phone = "invented phone",
            Evidence = ReadExample().Evidence with { Source = "other.md" }
        };
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => Validate(profile, ReadSample("resume.md")));
        Assert.Equal(string.Join(Environment.NewLine,
            "/evidence/source: expected the supplied CV filename.",
            "/evidence/quote: an extracted fact is absent from its evidence."), error.Message);
    }

    // Partial dates retain their precision; invalid dates and definite reversals are rejected.
    [Fact]
    public void Date_checks_preserve_precision_and_reject_invalid_chronology()
    {
        CandidateProfile profile = ReadExample();
        CandidateProfile partial = WithExperienceDates(profile, "2024", "2024-01");
        string source = SourceWithExperienceQuote(partial);
        CandidateProfile accepted = Validate(partial, source);
        Assert.Equal("2024", accepted.Experience[0].StartDate);
        Assert.Equal("2024-01", accepted.Experience[0].EndDate);

        CandidateProfile reversed = WithExperienceDates(profile, "2025", "2024");
        Assert.Contains("/experience/0/endDate: end date precedes start date.",
            Assert.Throws<InvalidDataException>(() => Validate(reversed, SourceWithExperienceQuote(reversed))).Message);

        CandidateProfile invalid = WithExperienceDates(profile, "2024-02-30", null);
        Assert.Contains("/experience/0/startDate: use a valid YYYY, YYYY-MM, or YYYY-MM-DD date, or null.",
            Assert.Throws<InvalidDataException>(() => Validate(invalid, SourceWithExperienceQuote(invalid))).Message);
    }

    // Normalization keeps the first supported skill and its evidence.
    [Fact]
    public void Skill_normalization_trims_names_and_keeps_the_first_duplicate()
    {
        CandidateProfile profile = ReadExample();
        Skill original = profile.Skills[0];
        profile = profile with
        {
            Skills = [original with { Name = " " + original.Name + " " }, original with { Name = original.Name.ToLowerInvariant() }]
        };
        Skill accepted = Assert.Single(Validate(profile, ReadSample("resume.md")).Skills);
        Assert.Equal(original, accepted);
    }

    // The integration cases must return useful typed collections.
    private static void AssertPopulatedProfile(CandidateProfile profile)
    {
        Assert.False(string.IsNullOrWhiteSpace(profile.Name));
        Assert.NotEmpty(profile.Experience);
        Assert.NotEmpty(profile.Education);
        Assert.NotEmpty(profile.Skills);
    }

    // The ambiguous source must not acquire dates, degrees, or proficiency through guessing.
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
        Assert.Equal(2, profile.Skills.Length);
    }

    // Construct supported date examples without changing the checked-in teaching resume.
    private static CandidateProfile WithExperienceDates(CandidateProfile profile, string? start, string? end)
    {
        Experience first = profile.Experience[0];
        string quote = $"{first.Employer} | {first.Role} | {start} to {end}";
        return profile with
        {
            Experience = [first with { StartDate = start, EndDate = end, DurationMonths = null, Evidence = new("resume.md", quote) }, .. profile.Experience.Skip(1)]
        };
    }

    // The constructed quote is a literal source span, just like the checked-in evidence.
    private static string SourceWithExperienceQuote(CandidateProfile profile) =>
        ReadSample("resume.md") + "\n" + profile.Experience[0].Evidence.Quote;

    // Reuse the application entry point for every local validation case.
    private static CandidateProfile Validate(CandidateProfile profile, string resume) =>
        ProfileValidator.ParseAndValidate(JsonSerializer.Serialize(profile, ProfileValidator.JsonOptions), resume, "resume.md");

    // A valid fixture supplies the unchanged fields for focused validation examples.
    private static CandidateProfile ReadExample() =>
        JsonSerializer.Deserialize<CandidateProfile>(ReadSample("profile.example.json"), ProfileValidator.JsonOptions)!;

    // Project references copy the same sample files into the test output.
    private static string ReadSample(string filename) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sample-data", filename));
}
