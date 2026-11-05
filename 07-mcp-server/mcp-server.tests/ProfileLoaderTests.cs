using Xunit;

namespace ResumeMcpServer.Tests;

public sealed class ProfileLoaderTests
{
    // Both fictional records must be copied so changing the selected ID works after a normal build.
    [Fact]
    public async Task Copied_profiles_retain_their_source_facts()
    {
        // Act
        List<ResumeProfile> resumeProfiles = await ProfileLoader.LoadAsync(Path.Combine(
            AppContext.BaseDirectory,
            "SampleData",
            "profiles.json"));

        // Assert
        Assert.Equal(
            new[] { "alex", "sam" },
            resumeProfiles.Select(resumeProfile => resumeProfile.IdString));
        Assert.Equal(
            new[] { "C#", ".NET", "PostgreSQL", "Docker" },
            resumeProfiles[0].SkillStrings);
        Assert.Equal(
            new[] { "TypeScript", "React", "SQLite", "Playwright" },
            resumeProfiles[1].SkillStrings);
    }
}
