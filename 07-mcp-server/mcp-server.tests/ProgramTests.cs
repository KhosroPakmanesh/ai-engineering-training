using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace ResumeMcpServer.Tests;

public sealed class ProgramTests
{
    // Inspect raw process streams too: a tolerant MCP client could otherwise conceal stray console text.
    [Fact]
    public async Task Standard_output_contains_only_protocol_and_diagnostics_use_standard_error()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryDirectory();
        var processStartInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        processStartInfo.ArgumentList.Add(Path.Combine(
            AppContext.BaseDirectory,
            "Server",
            "mcp-server.dll"));
        processStartInfo.Environment["RESUME_MCP_ALLOW_WRITE"] = "false";
        processStartInfo.Environment["RESUME_MCP_VERSIONS_DIRECTORY"] = temporaryDirectory.PathString;
        using Process process = Process.Start(processStartInfo)!;
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationTokenSource.Token);

        // Act
        try
        {
            await process.StandardInput.WriteLineAsync(
                """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"protocol-test","version":"1"}}}""");
            await process.StandardInput.FlushAsync(cancellationTokenSource.Token);
            string? initializeString = await process.StandardOutput.ReadLineAsync(cancellationTokenSource.Token);
            using JsonDocument initializeJsonDocument = JsonDocument.Parse(initializeString!);
            Assert.Equal(
                1,
                initializeJsonDocument.RootElement.GetProperty("id").GetInt32());

            await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
            await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""");
            await process.StandardInput.FlushAsync(cancellationTokenSource.Token);
            string? toolsString = await process.StandardOutput.ReadLineAsync(cancellationTokenSource.Token);
            using JsonDocument toolsJsonDocument = JsonDocument.Parse(toolsString!);
            Assert.Equal(
                2,
                toolsJsonDocument.RootElement.GetProperty("id").GetInt32());
            Assert.Equal(
                3,
                toolsJsonDocument.RootElement.GetProperty("result").GetProperty("tools").GetArrayLength());
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(cancellationTokenSource.Token);
        }

        // Assert
        Assert.Contains(
            "Local resume MCP server",
            await stderrTask);
        string remainingStdoutString = await process.StandardOutput.ReadToEndAsync(cancellationTokenSource.Token);
        Assert.True(string.IsNullOrWhiteSpace(remainingStdoutString));
    }
}
