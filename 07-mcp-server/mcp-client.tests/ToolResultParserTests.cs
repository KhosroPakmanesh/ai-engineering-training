using ModelContextProtocol.Protocol;
using Xunit;

namespace ResumeMcpClient.Tests;

public sealed class ToolResultParserTests
{
    // A JSON-looking payload is still a tool failure when MCP sets IsError.
    [Fact]
    public void Error_result_cannot_be_accepted_as_data()
    {
        // Arrange
        var callToolResult = new CallToolResult
        {
            IsError = true,
            Content =
            [
                new TextContentBlock
                {
                    Text = "[\"C#\"]"
                }
            ]
        };

        // Act
        // Assert
        Assert.Throws<InvalidDataException>(() => ToolResultParser.Parse(callToolResult));
    }

    // Successful MCP framing does not make malformed tool JSON acceptable.
    [Fact]
    public void Malformed_json_is_rejected()
    {
        // Arrange
        var callToolResult = new CallToolResult
        {
            Content =
            [
                new TextContentBlock
                {
                    Text = "not JSON"
                }
            ]
        };

        // Act
        // Assert
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => ToolResultParser.Parse(callToolResult));
    }
}
