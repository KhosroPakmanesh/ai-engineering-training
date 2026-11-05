using System.Text;
using System.Text.RegularExpressions;
using ModelContextProtocol;

namespace ResumeMcpServer;

public sealed class ResumeWriter(ServerSettings serverSettings)
{
    public const int MaxContentBytesInt = 16_384;

    // A tool supplies a version label, never a path or an authorization decision.
    public async Task<SavedResume> SaveAsync(
        string versionString,
        string markdownString,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!serverSettings.AllowWriteBool)
        {
            throw new McpException("Write permission is disabled for this server process.");
        }

        if (versionString is null || !Regex.IsMatch(
            versionString,
            "\\Av[0-9]{1,20}\\z",
            RegexOptions.CultureInvariant))
        {
            throw new McpException(
                "The version must be v followed by 1 to 20 digits, such as v2; paths and extensions are forbidden.");
        }

        if (string.IsNullOrWhiteSpace(markdownString)
            || Encoding.UTF8.GetByteCount(markdownString) > MaxContentBytesInt)
        {
            throw new McpException(
                $"Resume content must contain text and fit within {MaxContentBytesInt} UTF-8 bytes.");
        }

        // Reject directory links so a prepared versions folder cannot redirect a write elsewhere.
        string rootDirectoryString = Path.GetFullPath(serverSettings.VersionsDirectoryString);
        RejectDirectoryLink(rootDirectoryString);
        Directory.CreateDirectory(rootDirectoryString);
        RejectDirectoryLink(rootDirectoryString);
        string filenameString = versionString + ".md";
        string pathString = Path.Combine(
            rootDirectoryString,
            filenameString);

        // CreateNew rejects existing files and links atomically; it never overwrites a resume.
        bool createdBool = false;
        try
        {
            await using (var fileStream = new FileStream(
                pathString,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4_096,
                FileOptions.Asynchronous))
            {
                createdBool = true;
                await fileStream.WriteAsync(
                    Encoding.UTF8.GetBytes(markdownString),
                    cancellationToken);
            }
        }
        catch
        {
            if (createdBool)
            {
                File.Delete(pathString);
            }

            throw;
        }

        return new(filenameString);
    }

    // The process owner chooses the parent path; the managed versions directory must not be a link.
    private static void RejectDirectoryLink(string directoryString)
    {
        var directoryInfo = new DirectoryInfo(directoryString);
        if (directoryInfo.Exists && (directoryInfo.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new McpException("The versions directory must not be a symbolic link or junction.");
        }
    }
}
