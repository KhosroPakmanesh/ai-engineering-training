using ModelContextProtocol.Client;

namespace ResumeMcpClient.Tests;

internal sealed class McpTestSession : IAsyncDisposable
{
    public string DirectoryString
    {
        get;
    } = Path.Combine(
        Path.GetTempPath(),
        "resume-mcp-client-" + Guid.NewGuid().ToString("N"));
    public McpClient McpClient { get; private set; } = null!;

    // Close the transport before removing files so Windows releases all handles.
    public async ValueTask DisposeAsync()
    {
        try
        {
            await McpClient.DisposeAsync();
        }
        finally
        {
            Directory.Delete(
                DirectoryString,
                recursive: true);
        }
    }

    // Each test owns a genuine server process and an isolated destination directory.
    public static async Task<McpTestSession> StartAsync(bool allowWriteBool = false)
    {
        var mcpTestSession = new McpTestSession();
        Directory.CreateDirectory(mcpTestSession.DirectoryString);
        try
        {
            mcpTestSession.McpClient = await McpConnection.ConnectAsync(
                AppContext.BaseDirectory,
                allowWriteBool,
                mcpTestSession.DirectoryString);
            return mcpTestSession;
        }
        catch
        {
            Directory.Delete(
                mcpTestSession.DirectoryString,
                recursive: true);
            throw;
        }
    }
}
