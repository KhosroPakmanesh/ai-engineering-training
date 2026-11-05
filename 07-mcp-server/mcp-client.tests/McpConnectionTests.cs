using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace ResumeMcpClient.Tests;

public sealed class McpConnectionTests
{
    // These are local process tests, not provider integration tests; no AI settings are read.
    [Fact]
    public async Task Real_stdio_discovers_three_schemas_and_calls_the_skills_tool()
    {
        // Arrange
        await using McpTestSession mcpTestSession = await McpTestSession.StartAsync();

        // Act
        IList<McpClientTool> mcpClientTools = await mcpTestSession.McpClient.ListToolsAsync();

        // Assert
        Assert.Equal(
            new[] { "get_profile", "list_skills", "save_resume" },
            mcpClientTools.Select(mcpClientTool => mcpClientTool.Name).Order());
        McpClientTool saveMcpClientTool = mcpClientTools.Single(mcpClientTool => mcpClientTool.Name == "save_resume");
        Assert.Equal(
            new[] { "versionString", "markdownString" },
            saveMcpClientTool.JsonSchema.GetProperty("properties")
            .EnumerateObject().Select(jsonProperty => jsonProperty.Name));

        CallToolResult callToolResult = await mcpTestSession.McpClient.CallToolAsync(
            "list_skills",
            new Dictionary<string, object?>
            {
                ["profileIdString"] = "alex"
            });
        Assert.Equal(
            new[] { "C#", ".NET", "PostgreSQL", "Docker" },
            ToolResultParser.Parse(callToolResult)
            .EnumerateArray().Select(skillJsonElement => skillJsonElement.GetString()));
    }

    // An extra client-supplied permission flag cannot change the server's launch policy.
    [Fact]
    public async Task Direct_mcp_cannot_bypass_server_write_permission()
    {
        // Arrange
        await using McpTestSession mcpTestSession = await McpTestSession.StartAsync();

        // Act
        CallToolResult callToolResult = await mcpTestSession.McpClient.CallToolAsync(
            "save_resume",
            new Dictionary<string, object?>
            {
                ["versionString"] = "v2",
                ["markdownString"] = "# Fictional resume",
                ["allowWriteBool"] = true
            });

        // Assert
        Assert.True(callToolResult.IsError);
        InvalidDataException invalidDataException = Assert.Throws<InvalidDataException>(() =>
            ToolResultParser.Parse(callToolResult));
        Assert.Contains(
            "permission",
            invalidDataException.Message);
        Assert.Empty(Directory.GetFileSystemEntries(mcpTestSession.DirectoryString));
    }

    // The actual remote tool must apply the same filename and overwrite rules as the local writer.
    [Fact]
    public async Task Approved_server_saves_only_in_its_scope_and_rejects_overwrite()
    {
        // Arrange
        await using McpTestSession mcpTestSession = await McpTestSession.StartAsync(allowWriteBool: true);
        var argumentObjectsDictionary = new Dictionary<string, object?>
        {
            ["versionString"] = "v2",
            ["markdownString"] = "# Fictional resume"
        };

        // Act
        CallToolResult savedCallToolResult = await mcpTestSession.McpClient.CallToolAsync(
            "save_resume",
            argumentObjectsDictionary);

        // Assert
        Assert.Equal(
            "v2.md",
            ToolResultParser.Parse(savedCallToolResult).GetProperty("filename").GetString());
        Assert.Equal(
            "# Fictional resume",
            await File.ReadAllTextAsync(Path.Combine(
                mcpTestSession.DirectoryString,
                "v2.md")));
        CallToolResult repeatedCallToolResult = await mcpTestSession.McpClient.CallToolAsync(
            "save_resume",
            argumentObjectsDictionary);
        Assert.True(repeatedCallToolResult.IsError);
        Assert.Single(Directory.GetFiles(mcpTestSession.DirectoryString));
    }
}
