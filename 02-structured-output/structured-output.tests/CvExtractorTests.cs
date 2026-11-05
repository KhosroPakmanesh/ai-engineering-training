using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;
using Xunit.Abstractions;

public class CvExtractorTests(ITestOutputHelper outputITestOutputHelper)
{
    // Exercise the real request and every acceptance stage with a known fictional response.
    [Fact]
    public async Task Native_request_preserves_the_authored_schema_and_passes_the_complete_pipeline()
    {
        // Arrange
        string resumeString = ProfileTestData.ReadSample("resume.md");
        using var profileResponseHandler =
            new ProfileResponseHandler(ProfileTestData.ReadSample("profile.example.json"));
        using var httpClient = new HttpClient(profileResponseHandler);

        // Act
        AgentResponse<CandidateProfile> agentResponse = await CvExtractor.ExtractAsync(
            resumeString,
            "resume.md",
            new(
                "OpenRouter",
                "test-key",
                "test-model",
                new Uri("https://example.invalid/v1")),
            httpClient);
        JsonResumeSchemaValidator.ValidateSchema(agentResponse.Text);
        CandidateProfile candidateProfile = agentResponse.Result;
        ProfileValidator.ValidateProfile(
            candidateProfile,
            resumeString,
            "resume.md");
        CandidateProfile normalizedCandidateProfile = ProfileNormalizer.NormalizeProfile(candidateProfile);

        // Member names may change in C# while the displayed JSON must still satisfy the authored schema.
        JsonResumeSchemaValidator.ValidateSchema(JsonSerializer.Serialize(
            normalizedCandidateProfile,
            JsonSerializerOptions.Web));

        // Assert
        Assert.Equal(
            "Mira Example",
            normalizedCandidateProfile.NameString);
        using JsonDocument requestJsonDocument =
            JsonDocument.Parse(Assert.Single(profileResponseHandler.RequestBodyStrings));
        JsonElement schemaJsonElement =
            requestJsonDocument.RootElement.GetProperty("response_format").GetProperty("json_schema");
        using JsonDocument authoredJsonDocument =
            JsonDocument.Parse(File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory,
                "profile.schema.json")));
        Assert.True(JsonElement.DeepEquals(
            authoredJsonDocument.RootElement,
            schemaJsonElement.GetProperty("schema")));
        Assert.True(schemaJsonElement.GetProperty("strict").GetBoolean());
    }

    // A successful HTTP response still needs complete assistant text before schema validation.
    [Theory]
    [InlineData(
        "",
        "stop",
        "no response text")]
    [InlineData(
        "{}",
        "length",
        "truncated")]
    public async Task Native_request_rejects_empty_and_truncated_answers(
        string answerString,
        string finishReasonString,
        string expectedErrorString)
    {
        // Arrange
        using var profileResponseHandler = new ProfileResponseHandler(
            answerString,
            finishReasonString);
        using var httpClient = new HttpClient(profileResponseHandler);

        // Act
        InvalidDataException invalidDataException = await Assert.ThrowsAsync<InvalidDataException>(() =>
            CvExtractor.ExtractAsync(
                ProfileTestData.ReadSample("resume.md"),
                "resume.md",
                new(
                    "OpenRouter",
                    "test-key",
                    "test-model",
                    new Uri("https://example.invalid/v1")),
                httpClient));

        // Assert
        Assert.Contains(
            expectedErrorString,
            invalidDataException.Message);
        Assert.Single(profileResponseHandler.RequestBodyStrings);
    }

    // Native cancellation must reach the HTTP boundary and stop an already canceled caller locally.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Caller_cancellation_stops_the_extractor(bool cancelBeforeBool)
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        using var profileResponseHandler = new ProfileResponseHandler(
            "{}",
            beforeResponseAction: cancellationTokenSource.Cancel);
        using var httpClient = new HttpClient(profileResponseHandler);
        if (cancelBeforeBool)
        {
            cancellationTokenSource.Cancel();
        }

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CvExtractor.ExtractAsync(
                ProfileTestData.ReadSample("resume.md"),
                "resume.md",
                new(
                    "OpenRouter",
                    "test-key",
                    "test-model",
                    new Uri("https://example.invalid/v1")),
                httpClient,
                cancellationTokenSource.Token));

        // Assert
        Assert.Equal(
            cancelBeforeBool ? 0 : 1,
            profileResponseHandler.RequestBodyStrings.Count);
    }

    // A local fixture checks the post-request stages without credentials or a model call.
    [Fact]
    public void Example_response_passes_schema_conversion_evidence_and_normalization()
    {
        // Arrange
        string rawStringResume = ProfileTestData.ReadSample("resume.md");
        string jsonResumeString = ProfileTestData.ReadSample("profile.example.json");
        var rawAgentResponse = new AgentResponse(new ChatMessage(
            ChatRole.Assistant,
            jsonResumeString));

        // Act
        var agentResponse = new AgentResponse<CandidateProfile>(
            rawAgentResponse,
            JsonSerializerOptions.Web);

        // Assert
        Assert.Equal(
            jsonResumeString,
            agentResponse.Text);

        // Act
        JsonResumeSchemaValidator.ValidateSchema(agentResponse.Text);
        CandidateProfile candidateProfile = agentResponse.Result;
        ProfileValidator.ValidateProfile(
            candidateProfile,
            rawStringResume,
            "resume.md");
        candidateProfile = ProfileNormalizer.NormalizeProfile(candidateProfile);

        // Assert
        Assert.Equal(
            "Mira Example",
            candidateProfile.NameString);
        Assert.Equal(
            2,
            candidateProfile.Experiences.Count);
    }

    // Model responses pass through the same local checks and cleanup as Program.cs.
    [Theory]
    [InlineData(
        "resume.md",
        false)]
    [InlineData(
        "resume-ambiguous.md",
        true)]
    [Trait(
        "Category",
        "Integration")]
    public async Task Extract_returns_a_typed_profile_and_preserves_unknowns(
        string resumeFileNameString,
        bool hasMissingFactsBool)
    {
        // Arrange
        string rawStringResume = ProfileTestData.ReadSample(resumeFileNameString);

        // Act
        AgentResponse<CandidateProfile> agentResponse = await CvExtractor.ExtractAsync(
            rawStringResume,
            resumeFileNameString);
        JsonResumeSchemaValidator.ValidateSchema(agentResponse.Text);
        CandidateProfile candidateProfile = agentResponse.Result;
        ProfileValidator.ValidateProfile(
            candidateProfile,
            rawStringResume,
            resumeFileNameString);
        candidateProfile = ProfileNormalizer.NormalizeProfile(candidateProfile);

        JsonSerializerOptions jsonSerializerOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        outputITestOutputHelper.WriteLine(JsonSerializer.Serialize(
            candidateProfile,
            jsonSerializerOptions));

        AssertPopulatedProfile(candidateProfile);

        // Assert
        if (hasMissingFactsBool)
        {
            AssertMissingFacts(candidateProfile);
        }
        else
        {
            Assert.Equal(
                2,
                candidateProfile.Experiences.Count);
            Assert.Contains(
                candidateProfile.Educations,
                itemEducation => itemEducation.DegreeString is not null);
        }
    }

    // Both resumes must produce a name and nonempty experience, education, and skill lists.
    private static void AssertPopulatedProfile(CandidateProfile candidateProfile)
    {
        Assert.False(string.IsNullOrWhiteSpace(candidateProfile.NameString));
        Assert.NotEmpty(candidateProfile.Experiences);
        Assert.NotEmpty(candidateProfile.Educations);
        Assert.NotEmpty(candidateProfile.Skills);
    }

    // Missing and ambiguous source facts must remain unknown.
    private static void AssertMissingFacts(CandidateProfile candidateProfile)
    {
        Assert.Null(candidateProfile.PhoneString);
        Assert.All(
            candidateProfile.Educations,
            itemEducation => Assert.Null(itemEducation.DegreeString));
        Assert.All(
            candidateProfile.Experiences,
            itemExperience =>
        {
            Assert.Null(itemExperience.StartDateString);
            Assert.Null(itemExperience.EndDateString);
            Assert.Null(itemExperience.DurationMonthsInt);
        });
        Assert.All(
            candidateProfile.Skills,
            itemSkill => Assert.Null(itemSkill.ProficiencyString));
        Assert.Equal(
            2,
            candidateProfile.Skills.Count);
    }
}
