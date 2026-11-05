using Xunit;

namespace ResumeMcpServer.Tests;

public sealed class ProfileValidatorTests
{
    // Valid fictional facts remain unchanged; validation does not trim or repair them.
    [Fact]
    public void Valid_facts_are_preserved()
    {
        // Arrange
        var skillStrings = new List<string> { "C#", ".NET" };
        List<ResumeProfile> resumeProfiles =
        [
            new(
            "alex",
            "Alex (fictional)",
            "Built a sample API.",
            skillStrings)
        ];

        // Act
        ProfileValidator.Validate(resumeProfiles);

        // Assert
        Assert.Same(
            skillStrings,
            resumeProfiles[0].SkillStrings);
    }

    // Independent errors should report their field paths together.
    [Fact]
    public void Duplicate_ids_and_missing_facts_are_reported_together()
    {
        // Arrange
        List<ResumeProfile> resumeProfiles =
        [
            new(
            "alex",
            "Alex",
            "Example",
            ["C#"]),
            new(
                "alex",
                "",
                "",
                [""])
        ];

        // Act
        InvalidDataException invalidDataException = Assert.Throws<InvalidDataException>(() =>
            ProfileValidator.Validate(resumeProfiles));

        // Assert
        Assert.Contains(
            "profiles[1].id",
            invalidDataException.Message);
        Assert.Contains(
            "profiles[1].name",
            invalidDataException.Message);
        Assert.Contains(
            "profiles[1].summary",
            invalidDataException.Message);
        Assert.Contains(
            "profiles[1].skills",
            invalidDataException.Message);
    }
}
