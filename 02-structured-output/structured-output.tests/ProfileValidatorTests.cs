using Xunit;

// Run these classes one at a time because ProfileValidator stores shared data.
[Collection("Profile validation")]
public class ProfileValidatorTests
{
    // Correct JSON structure does not guarantee that facts match the supplied source.
    [Fact]
    public void Source_and_text_facts_must_match_their_evidence()
    {
        CandidateProfile profile = ProfileTestData.ReadExample() with
        {
            Phone = "invented phone",
            Evidence = ProfileTestData.ReadExample().Evidence with { Source = "other.md" }
        };
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => ProfileValidator.ValidateProfile(profile, ProfileTestData.ReadSample("resume.md"), "resume.md"));
        Assert.Equal(string.Join(Environment.NewLine,
            "/evidence/source: expected the supplied CV filename.",
            "/evidence/quote: an extracted fact is absent from its evidence."), error.Message);

        // A rejected profile must not leave errors behind for the next validation.
        CandidateProfile accepted = ProfileTestData.ReadExample();
        ProfileValidator.ValidateProfile(accepted, ProfileTestData.ReadSample("resume.md"), "resume.md");
        Assert.Equal("Mira Example", accepted.Name);
    }

    // A quote must copy the resume exactly, even if changed wording contains the same facts.
    [Fact]
    public void Evidence_quotes_must_exist_verbatim_in_the_resume()
    {
        CandidateProfile profile = ProfileTestData.ReadExample();
        profile = profile with
        {
            Evidence = profile.Evidence with { Quote = profile.Evidence.Quote + " Invented source sentence." }
        };

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ProfileValidator.ValidateProfile(profile, ProfileTestData.ReadSample("resume.md"), "resume.md"));
        Assert.Contains("/evidence/quote: expected an exact, nonempty span of the CV.", error.Message);
    }

    // Do not calculate months of employment from dates when the resume does not state the duration.
    [Fact]
    public void Employment_duration_must_be_explicitly_stated()
    {
        CandidateProfile profile = ProfileTestData.ReadExample();
        profile = profile with
        {
            Experience = [profile.Experience[0] with { DurationMonths = 26 }, .. profile.Experience.Skip(1)]
        };

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ProfileValidator.ValidateProfile(profile, ProfileTestData.ReadSample("resume.md"), "resume.md"));
        Assert.Contains("/experience/0/durationMonths: duration must be explicitly stated in months, or null.", error.Message);
    }

    // Keep dates as given, including years alone; reject invalid dates and clearly reversed start and end dates.
    [Fact]
    public void Date_checks_preserve_precision_and_reject_invalid_chronology()
    {
        CandidateProfile profile = ProfileTestData.ReadExample();
        CandidateProfile partial = WithExperienceDates(profile, "2024", "2024-01");
        ProfileValidator.ValidateProfile(partial, SourceWithExperienceQuote(partial), "resume.md");
        Assert.Equal("2024", partial.Experience[0].StartDate);
        Assert.Equal("2024-01", partial.Experience[0].EndDate);

        CandidateProfile reversed = WithExperienceDates(profile, "2025", "2024");
        Assert.Contains("/experience/0/endDate: end date precedes start date.",
            Assert.Throws<InvalidDataException>(() =>
                ProfileValidator.ValidateProfile(reversed, SourceWithExperienceQuote(reversed), "resume.md")).Message);

        CandidateProfile invalid = WithExperienceDates(profile, "2024-02-30", null);
        Assert.Contains("/experience/0/startDate: use a valid YYYY, YYYY-MM, or YYYY-MM-DD date, or null.",
            Assert.Throws<InvalidDataException>(() =>
                ProfileValidator.ValidateProfile(invalid, SourceWithExperienceQuote(invalid), "resume.md")).Message);
    }

    // Change one job entry's dates and leave the other example facts unchanged.
    private static CandidateProfile WithExperienceDates(CandidateProfile profile, string? start, string? end)
    {
        Experience first = profile.Experience[0];
        string quote = $"{first.Employer} | {first.Role} | {start} to {end}";
        return profile with
        {
            Experience = [first with { StartDate = start, EndDate = end, DurationMonths = null, Evidence = new("resume.md", quote) }, .. profile.Experience.Skip(1)]
        };
    }

    // Put the date quote in the test resume too, so the exact-quote check can pass.
    private static string SourceWithExperienceQuote(CandidateProfile profile) =>
        ProfileTestData.ReadSample("resume.md") + "\n" + profile.Experience[0].Evidence.Quote;
}
