using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace ResumeMcpClient;

public static class ResumeSaver
{
    // Keep confirmation outside tool arguments. A declined save must not even start a writable server.
    public static async Task<string?> SaveAsync(
        string folderString,
        string versionsDirectoryString,
        ResumeDraft resumeDraft,
        string versionString,
        TextReader confirmationTextReader,
        CancellationToken cancellationToken = default)
    {
        OutputPrinter.PrintSavePreview(
            resumeDraft,
            versionString,
            versionsDirectoryString);
        string? confirmationString = await confirmationTextReader.ReadLineAsync(cancellationToken);
        if (confirmationString != "SAVE")
        {
            return null;
        }

        using var saveCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        saveCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(AiClientFactory.RequestTimeoutSeconds));
        await using McpClient writableMcpClient = await McpConnection.ConnectAsync(
            folderString,
            allowWriteBool: true,
            versionsDirectoryString,
            saveCancellationTokenSource.Token);
        CallToolResult callToolResult = await writableMcpClient.CallToolAsync(
            "save_resume",
            new Dictionary<string, object?>
            {
                ["versionString"] = versionString,
                ["markdownString"] = resumeDraft.MarkdownString
            },
            cancellationToken: saveCancellationTokenSource.Token);
        string? filenameString = ToolResultParser.Parse(callToolResult).GetProperty("filename").GetString();
        if (filenameString != versionString + ".md")
        {
            throw new InvalidDataException("The save result did not identify the approved version filename.");
        }

        return Path.Combine(
            versionsDirectoryString,
            filenameString);
    }
}
