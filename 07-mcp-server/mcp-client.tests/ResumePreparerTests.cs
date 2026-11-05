using System.Text.Json;
using ModelContextProtocol.Protocol;
using Xunit;

namespace ResumeMcpClient.Tests;

public sealed class ResumePreparerTests
{
    // The second fixture must produce source-based Markdown without inventing achievements or dates.
    [Fact]
    public async Task Second_profile_produces_a_different_source_based_draft()
    {
        // Arrange
        await using McpTestSession mcpTestSession = await McpTestSession.StartAsync();
        CallToolResult callToolResult = await mcpTestSession.McpClient.CallToolAsync(
            "get_profile",
            new Dictionary<string, object?>
            {
                ["profileIdString"] = "sam"
            });

        // Act
        ResumeDraft resumeDraft = ResumePreparer.Prepare(
            ToolResultParser.Parse(callToolResult),
            "sam");

        // Assert
        Assert.Equal(
            new[] { "TypeScript", "React", "SQLite", "Playwright" },
            resumeDraft.SkillStrings);
        Assert.StartsWith(
            "# Sam Rivera (fictional)",
            resumeDraft.MarkdownString);
        Assert.Contains(
            "community garden dashboard",
            resumeDraft.MarkdownString);
        Assert.DoesNotContain(
            "PostgreSQL",
            resumeDraft.MarkdownString);
    }

    // Source selection cannot silently switch profiles across the process boundary.
    [Fact]
    public void A_reply_for_another_profile_is_rejected()
    {
        // Arrange
        JsonElement profileJsonElement =
            JsonElement.Parse("""{"id":"sam","name":"Sam","summary":"Fictional","skills":["React"]}""");

        // Act
        // Assert
        Assert.Throws<InvalidDataException>(() => ResumePreparer.Prepare(
            profileJsonElement,
            "alex"));
    }

    // JSON can contain null even when the C# list declares non-null skill names.
    [Theory]
    [InlineData("null")]
    [InlineData("[null]")]
    public void Null_skills_are_rejected_before_preparing_markdown(string skillsJsonString)
    {
        // Arrange
        JsonElement profileJsonElement = JsonElement.Parse($$"""
            {"id":"alex","name":"Alex (fictional)","summary":"Built a sample API.","skills":{{skillsJsonString}}}
            """);

        // Act
        InvalidDataException invalidDataException = Assert.Throws<InvalidDataException>(() =>
            ResumePreparer.Prepare(
                profileJsonElement,
                "alex"));

        // Assert
        Assert.Contains(
            "skills",
            invalidDataException.Message);
    }
}
