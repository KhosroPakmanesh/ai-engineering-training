using System.Diagnostics;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace ResumeMcpServer.Tests;

public sealed class ServerSettingsTests
{
    // Omit permission in the child environment so the real startup path must default to denying writes.
    [Fact]
    public async Task Missing_permission_denies_a_valid_direct_save()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryDirectory();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Dictionary<string, string?> environmentStringsDictionary =
            StdioClientTransportOptions.GetDefaultEnvironmentVariables();
        environmentStringsDictionary.Remove("RESUME_MCP_ALLOW_WRITE");
        environmentStringsDictionary["RESUME_MCP_VERSIONS_DIRECTORY"] = temporaryDirectory.PathString;
        var stdioClientTransport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "DefaultPermissionTest",
            Command = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            Arguments =
            [
                Path.Combine(
                AppContext.BaseDirectory,
                "Server",
                "mcp-server.dll")
            ],
            InheritEnvironmentVariables = false,
            EnvironmentVariables = environmentStringsDictionary
        });
        await using McpClient mcpClient = await McpClient.CreateAsync(
            stdioClientTransport,
            cancellationToken: cancellationTokenSource.Token);

        // Act
        CallToolResult callToolResult = await mcpClient.CallToolAsync(
            "save_resume",
            new Dictionary<string, object?>
            {
                ["versionString"] = "v2",
                ["markdownString"] = "# Fictional resume"
            },
            cancellationToken: cancellationTokenSource.Token);

        // Assert
        Assert.True(callToolResult.IsError);
        string errorString = string.Join(
            "\n",
            callToolResult.Content.OfType<TextContentBlock>()
            .Select(textContentBlock => textContentBlock.Text));
        Assert.Contains(
            "Write permission is disabled",
            errorString);
        Assert.Empty(Directory.GetFileSystemEntries(temporaryDirectory.PathString));
    }

    // Change only one setting per child process; no test mutates the test runner's environment.
    [Theory]
    [InlineData(
        "yes",
        false,
        "RESUME_MCP_ALLOW_WRITE")]
    [InlineData(
        "false",
        true,
        "RESUME_MCP_VERSIONS_DIRECTORY")]
    public async Task Invalid_settings_stop_startup_before_serving_tools(
        string permissionString,
        bool relativeDirectoryBool,
        string expectedSettingString)
    {
        // Arrange
        using var temporaryDirectory = new TemporaryDirectory();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var processStartInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = temporaryDirectory.PathString
        };
        processStartInfo.ArgumentList.Add(Path.Combine(
            AppContext.BaseDirectory,
            "Server",
            "mcp-server.dll"));
        processStartInfo.Environment["RESUME_MCP_ALLOW_WRITE"] = permissionString;
        processStartInfo.Environment["RESUME_MCP_VERSIONS_DIRECTORY"] =
            relativeDirectoryBool ? "versions" : temporaryDirectory.PathString;
        using Process process = Process.Start(processStartInfo)!;
        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationTokenSource.Token);
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(cancellationTokenSource.Token);

        // Act
        try
        {
            await process.WaitForExitAsync(cancellationTokenSource.Token);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(cancellationTokenSource.Token);
            }
        }

        // Assert
        Assert.Equal(
            1,
            process.ExitCode);
        Assert.Contains(
            expectedSettingString,
            await standardErrorTask);
        Assert.Equal(
            string.Empty,
            await standardOutputTask);
        Assert.Empty(Directory.GetFileSystemEntries(temporaryDirectory.PathString));
    }
}
