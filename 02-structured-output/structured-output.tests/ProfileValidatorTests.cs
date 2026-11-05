using Xunit;

public class ProfileValidatorTests
{
    // Correct JSON structure does not guarantee that facts match the supplied source.
    [Fact]
    public void Source_and_text_facts_must_match_their_evidence()
    {
        // Arrange
        CandidateProfile candidateProfile = ProfileTestData.ReadExample() with
        {
            PhoneString = "invented phone",
            Evidence = ProfileTestData.ReadExample().Evidence with
            {
                SourceString = "other.md"
            }
        };

        // Act
        InvalidDataException errorInvalidDataException = Assert.Throws<InvalidDataException>(() =>
            ProfileValidator.ValidateProfile(
                candidateProfile,
                ProfileTestData.ReadSample("resume.md"),
                "resume.md"));

        // Assert
        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "/evidence/source: expected the supplied CV filename.",
                "/evidence/quote: an extracted fact is absent from its evidence."),
            errorInvalidDataException.Message);

        // A rejected profile must not leave errors behind for the next validation.
        CandidateProfile acceptedCandidateProfile = ProfileTestData.ReadExample();
        ProfileValidator.ValidateProfile(
            acceptedCandidateProfile,
            ProfileTestData.ReadSample("resume.md"),
            "resume.md");
        Assert.Equal(
            "Mira Example",
            acceptedCandidateProfile.NameString);
    }

    // A quote must copy the resume exactly, even if changed wording contains the same facts.
    [Fact]
    public void Evidence_quotes_must_exist_verbatim_in_the_resume()
    {
        // Arrange
        CandidateProfile candidateProfile = ProfileTestData.ReadExample();
        candidateProfile = candidateProfile with
        {
            Evidence = candidateProfile.Evidence with
            {
                QuoteString = candidateProfile.Evidence.QuoteString + " Invented source sentence."
            }
        };

        // Act
        InvalidDataException errorInvalidDataException = Assert.Throws<InvalidDataException>(() =>
            ProfileValidator.ValidateProfile(
                candidateProfile,
                ProfileTestData.ReadSample("resume.md"),
                "resume.md"));

        // Assert
        Assert.Contains(
            "/evidence/quote: expected an exact, nonempty span of the CV.",
            errorInvalidDataException.Message);
    }

    // Do not calculate months of employment from dates when the resume does not state the duration.
    [Fact]
    public void Employment_duration_must_be_explicitly_stated()
    {
        // Arrange
        CandidateProfile candidateProfile = ProfileTestData.ReadExample();
        candidateProfile = candidateProfile with
        {
            Experiences =
            [
                candidateProfile.Experiences[0] with
                {
                    DurationMonthsInt = 26
                },
                .. candidateProfile.Experiences.Skip(1)
            ]
        };

        // Act
        InvalidDataException errorInvalidDataException = Assert.Throws<InvalidDataException>(() =>
            ProfileValidator.ValidateProfile(
                candidateProfile,
                ProfileTestData.ReadSample("resume.md"),
                "resume.md"));

        // Assert
        Assert.Contains(
            "/experience/0/durationMonths: duration must be explicitly stated in months, or null.",
            errorInvalidDataException.Message);
    }

    // Keep dates as given, including years alone; reject invalid dates and clearly reversed start and end dates.
    [Fact]
    public void Date_checks_preserve_precision_and_reject_invalid_chronology()
    {
        // Arrange
        CandidateProfile candidateProfile = ProfileTestData.ReadExample();
        CandidateProfile partialCandidateProfile = WithExperienceDates(
            candidateProfile,
            "2024",
            "2024-01");

        // Act
        ProfileValidator.ValidateProfile(
            partialCandidateProfile,
            SourceWithExperienceQuote(partialCandidateProfile),
            "resume.md");

        // Assert
        Assert.Equal(
            "2024",
            partialCandidateProfile.Experiences[0].StartDateString);
        Assert.Equal(
            "2024-01",
            partialCandidateProfile.Experiences[0].EndDateString);

        CandidateProfile reversedCandidateProfile = WithExperienceDates(
            candidateProfile,
            "2025",
            "2024");
        Assert.Contains(
            "/experience/0/endDate: end date precedes start date.",
            Assert.Throws<InvalidDataException>(() =>
                ProfileValidator.ValidateProfile(
                    reversedCandidateProfile,
                    SourceWithExperienceQuote(reversedCandidateProfile),
                    "resume.md")).Message);

        CandidateProfile invalidCandidateProfile = WithExperienceDates(
            candidateProfile,
            "2024-02-30",
            null);
        Assert.Contains(
            "/experience/0/startDate: use a valid YYYY, YYYY-MM, or YYYY-MM-DD date, or null.",
            Assert.Throws<InvalidDataException>(() =>
                ProfileValidator.ValidateProfile(
                    invalidCandidateProfile,
                    SourceWithExperienceQuote(invalidCandidateProfile),
                    "resume.md")).Message);
    }

    // Start different source checks together; each accepted or rejected profile must keep its own result.
    [Fact]
    public async Task Concurrent_validations_keep_their_own_sources_and_errors()
    {
        // Arrange
        List<(string NameString, string FilenameString, bool InvalidBool)> valueTuples =
        [
            ("Mira Example", "mira.md", false),
            ("Alex Sample", "alex.md", true),
            ("Robin Example", "robin.md", false),
            ("Jamie Sample", "jamie.md", true)
        ];
        using var startGateBarrier = new Barrier(valueTuples.Count);

        // Act
        // Dedicated workers can all reach the gate even when the test runner's thread pool is busy.
        List<Task<Exception?>> checksTasks = valueTuples
            .Select(testCaseValueTuple => Task.Factory.StartNew<Exception?>(
                () =>
        {
            CandidateProfile candidateProfile = new(
                NameString: testCaseValueTuple.NameString,
                EmailString: null,
                PhoneString: testCaseValueTuple.InvalidBool ? "unsupported phone" : null,
                Evidence: new(
                    testCaseValueTuple.FilenameString,
                    testCaseValueTuple.NameString),
                Experiences: [],
                Educations: [],
                Skills: []);

            startGateBarrier.SignalAndWait();
            return Record.Exception(() => ProfileValidator.ValidateProfile(
                candidateProfile,
                testCaseValueTuple.NameString,
                testCaseValueTuple.FilenameString));
        },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToList();

        Exception?[] outcomesExceptions = await Task.WhenAll(checksTasks);

        // Assert
        for (int indexInt = 0; indexInt < valueTuples.Count; indexInt++)
        {
            if (valueTuples[indexInt].InvalidBool)
            {
                InvalidDataException errorInvalidDataException =
                    Assert.IsType<InvalidDataException>(outcomesExceptions[indexInt]);
                string errorLineString = Assert.Single(errorInvalidDataException.Message.Split(Environment.NewLine));
                Assert.StartsWith(
                    "/evidence/quote:",
                    errorLineString);
                Assert.Contains(
                    "an extracted fact is absent",
                    errorLineString);
            }
            else
            {
                Assert.Null(outcomesExceptions[indexInt]);
            }
        }
    }

    // Change one job entry's dates and leave the other example facts unchanged.
    private static CandidateProfile WithExperienceDates(
        CandidateProfile candidateProfile,
        string? startString,
        string? endString)
    {
        Experience firstExperience = candidateProfile.Experiences[0];
        string quoteString =
            $"{firstExperience.EmployerString} | {firstExperience.RoleString} | {startString} to {endString}";
        return candidateProfile with
        {
            Experiences =
            [
                firstExperience with
                {
                    StartDateString = startString,
                    EndDateString = endString,
                    DurationMonthsInt = null,
                    Evidence = new(
                    "resume.md",
                    quoteString)
                },
                .. candidateProfile.Experiences.Skip(1)
            ]
        };
    }

    // Put the date quote in the test resume too, so the exact-quote check can pass.
    private static string SourceWithExperienceQuote(CandidateProfile candidateProfile)
    {
        return ProfileTestData.ReadSample("resume.md") + "\n" + candidateProfile.Experiences[0].Evidence.QuoteString;
    }
}
