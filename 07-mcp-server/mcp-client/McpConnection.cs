using ModelContextProtocol.Client;

namespace ResumeMcpClient;

public static class McpConnection
{
    // Launch a built DLL so build messages never enter the MCP protocol stream.
    public static async Task<McpClient> ConnectAsync(
        string folderString,
        bool allowWriteBool,
        string versionsDirectoryString,
        CancellationToken cancellationToken = default)
    {
        string serverPathString = Path.GetFullPath(Path.Combine(
            folderString,
            "Server",
            "mcp-server.dll"));
        if (!File.Exists(serverPathString))
        {
            throw new FileNotFoundException(
                "Build mcp-client to copy the independently compiled MCP server.",
                serverPathString);
        }

        // Pass only normal process settings and this server's scope; no AI credentials are needed.
        Dictionary<string, string?> environmentStringsDictionary =
            StdioClientTransportOptions.GetDefaultEnvironmentVariables();
        environmentStringsDictionary["RESUME_MCP_ALLOW_WRITE"] = allowWriteBool ? "true" : "false";
        environmentStringsDictionary["RESUME_MCP_VERSIONS_DIRECTORY"] = Path.GetFullPath(versionsDirectoryString);
        var stdioClientTransport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "FictionalResumeServer",
            Command = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            Arguments = [serverPathString],
            InheritEnvironmentVariables = false,
            EnvironmentVariables = environmentStringsDictionary,
            StandardErrorLines = errorString => OutputPrinter.PrintServerLog(errorString)
        });

        using var setupCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        setupCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(AiClientFactory.RequestTimeoutSeconds));
        return await McpClient.CreateAsync(
            stdioClientTransport,
            cancellationToken: setupCancellationTokenSource.Token);
    }
}
