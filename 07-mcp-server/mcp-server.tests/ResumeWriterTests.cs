using ModelContextProtocol;
using Xunit;

namespace ResumeMcpServer.Tests;

public sealed class ResumeWriterTests
{
    // A valid request must still fail when the process owner has disabled writes.
    [Fact]
    public async Task Disabled_permission_prevents_any_file_creation()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryDirectory();
        var resumeWriter = new ResumeWriter(new(
            false,
            temporaryDirectory.PathString));

        // Act
        McpException mcpException = await Assert.ThrowsAsync<McpException>(() => resumeWriter.SaveAsync(
            "v2",
            "# Fictional resume"));

        // Assert
        Assert.Contains(
            "permission",
            mcpException.Message);
        Assert.Empty(Directory.GetFileSystemEntries(temporaryDirectory.PathString));
    }

    // Version validation must work the same way on Windows and Unix, regardless of path syntax.
    [Theory]
    [InlineData("../v2")]
    [InlineData("..\\v2")]
    [InlineData("/tmp/v2")]
    [InlineData("C:\\outside\\v2")]
    [InlineData("v2.md")]
    [InlineData("v2:stream")]
    [InlineData("v2\n")]
    [InlineData("v")]
    [InlineData("v123456789012345678901")]
    public async Task Paths_extensions_and_invalid_versions_are_denied(string versionString)
    {
        // Arrange
        using var temporaryDirectory = new TemporaryDirectory();
        var resumeWriter = new ResumeWriter(new(
            true,
            temporaryDirectory.PathString));

        // Act
        McpException mcpException = await Assert.ThrowsAsync<McpException>(() => resumeWriter.SaveAsync(
            versionString,
            "# Fictional resume"));

        // Assert
        Assert.Contains(
            "version",
            mcpException.Message);
        Assert.Empty(Directory.GetFileSystemEntries(temporaryDirectory.PathString));
    }

    // CreateNew must preserve the original bytes when a second save requests the same filename.
    [Fact]
    public async Task Allowed_write_is_scoped_and_cannot_overwrite_a_version()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryDirectory();
        var resumeWriter = new ResumeWriter(new(
            true,
            temporaryDirectory.PathString));

        // Act
        SavedResume savedResume = await resumeWriter.SaveAsync(
            "v2",
            "# Fictional resume\nC#\n");

        // Assert
        Assert.Equal(
            "v2.md",
            savedResume.FilenameString);
        string pathString = Path.Combine(
            temporaryDirectory.PathString,
            "v2.md");
        Assert.Equal(
            "# Fictional resume\nC#\n",
            await File.ReadAllTextAsync(pathString));
        await Assert.ThrowsAsync<IOException>(() => resumeWriter.SaveAsync(
            "v2",
            "Replacement"));
        Assert.Equal(
            "# Fictional resume\nC#\n",
            await File.ReadAllTextAsync(pathString));
        Assert.Single(Directory.GetFiles(temporaryDirectory.PathString));
    }

    // The limit measures encoded bytes, not C# characters; a non-ASCII character can use several bytes.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Empty_and_oversized_utf8_content_are_denied(bool oversizedBool)
    {
        // Arrange
        using var temporaryDirectory = new TemporaryDirectory();
        var resumeWriter = new ResumeWriter(new(
            true,
            temporaryDirectory.PathString));
        string markdownString = oversizedBool ? new string(
            '界',
            6_000) : " \n";

        // Act
        McpException mcpException = await Assert.ThrowsAsync<McpException>(() => resumeWriter.SaveAsync(
            "v2",
            markdownString));

        // Assert
        Assert.Contains(
            "content",
            mcpException.Message);
        Assert.Empty(Directory.GetFileSystemEntries(temporaryDirectory.PathString));
    }

    // A canceled call must not leave an empty version behind.
    [Fact]
    public async Task Cancellation_before_a_save_creates_no_file()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryDirectory();
        var resumeWriter = new ResumeWriter(new(
            true,
            temporaryDirectory.PathString));
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resumeWriter.SaveAsync(
            "v2",
            "# Fictional resume",
            cancellationTokenSource.Token));

        // Assert
        Assert.Empty(Directory.GetFileSystemEntries(temporaryDirectory.PathString));
    }
}
