using Xunit;

namespace ResumeMcpClient.Tests;

public sealed class ResumeSaverTests
{
    // A declined response and end of input both stop before even looking for a server executable.
    [Theory]
    [InlineData("no\n")]
    [InlineData("")]
    [InlineData("save\n")]
    public async Task Unconfirmed_save_does_not_start_a_server(string confirmationString)
    {
        // Arrange
        var resumeDraft = new ResumeDraft(
            "alex",
            ["C#"],
            "# Fictional resume");
        using var confirmationStringReader = new StringReader(confirmationString);

        // Act
        string? savedPathString = await ResumeSaver.SaveAsync(
            "missing-server",
            "unused",
            resumeDraft,
            "v2",
            confirmationStringReader);

        // Assert
        Assert.Null(savedPathString);
    }

    // Confirmation is application input, not a field that an MCP caller or model can set.
    [Fact]
    public async Task Confirmation_starts_a_scoped_server_and_saves_the_reviewed_text()
    {
        // Arrange
        await using McpTestSession mcpTestSession = await McpTestSession.StartAsync();
        var resumeDraft = new ResumeDraft(
            "alex",
            ["C#"],
            "# Alex (fictional)\n\n- C#\n");
        using var confirmationStringReader = new StringReader("SAVE\n");

        // Act
        string? savedPathString = await ResumeSaver.SaveAsync(
            AppContext.BaseDirectory,
            mcpTestSession.DirectoryString,
            resumeDraft,
            "v2",
            confirmationStringReader);

        // Assert
        Assert.Equal(
            Path.Combine(
                mcpTestSession.DirectoryString,
                "v2.md"),
            savedPathString);
        Assert.Equal(
            resumeDraft.MarkdownString,
            await File.ReadAllTextAsync(savedPathString!));
    }
}
