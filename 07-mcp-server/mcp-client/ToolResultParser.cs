using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace ResumeMcpClient;

public static class ToolResultParser
{
    // MCP tool failures can arrive as successful protocol replies with IsError set.
    public static JsonElement Parse(CallToolResult callToolResult)
    {
        string textString = string.Join(
            "\n",
            callToolResult.Content.OfType<TextContentBlock>()
            .Select(textContentBlock => textContentBlock.Text));
        if (callToolResult.IsError == true)
        {
            throw new InvalidDataException($"MCP tool failed: {textString}");
        }

        return JsonElement.Parse(textString);
    }
}
